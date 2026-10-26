
using Godot;

public partial class Shotgun : Node3D
{
	[ExportGroup("Посилання")]
	[Export] public NodePath CameraPath { get; set; } = new NodePath("..");
	[Export] public NodePath AmmoLabelPath { get; set; } =
		new NodePath("../../CanvasLayer/AmmoLabel");

	[ExportGroup("Боєприпаси")]
	[Export] public int MagazineCapacity { get; set; } = 8;
	[Export] public int StartingReserveAmmo { get; set; } = 32;

	[ExportGroup("Параметри дробовика")]
	[Export] public int PelletsPerShot { get; set; } = 8;
	[Export] public float DamagePerPellet { get; set; } = 3.0f;
	[Export] public float Spread { get; set; } = 0.035f;
	[Export] public float Range { get; set; } = 60.0f;
	[Export] public float FireCooldown { get; set; } = 0.8f;
	[Export] public float ReloadShellTime { get; set; } = 0.65f;
	[Export(PropertyHint.Layers3DPhysics)]
	public uint CollisionMask { get; set; } = 1;

	[ExportGroup("Віддача")]
	[Export] public float RecoilDistance { get; set; } = 0.12f;
	[Export] public float RecoilRecoverySpeed { get; set; } = 12.0f;

	[ExportGroup("Візуал")]
	[Export] public PackedScene WeaponScene { get; set; }
	private Node3D _weaponModel;

	private Camera3D _camera;
	private Label _ammoLabel;
	private CharacterBody3D _player;

	private int _ammoInMagazine;
	private int _reserveAmmo;

	private bool _isReloading;
	private float _reloadTimer;
	private float _fireTimer;
	private float _recoilAmount;

	private Vector3 _basePosition;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_camera = GetNodeOrNull<Camera3D>(CameraPath);

		if (_camera == null)
		{
			GD.PushError(
                "Shotgun: не знайдено Camera3D. Перевір CameraPath."
			);
			SetProcess(false);
			SetProcessInput(false);
			return;
		}
		if (WeaponScene != null)
		{
			_weaponModel = WeaponScene.Instantiate<Node3D>();
			AddChild(_weaponModel);
		}
		else
		{
			GD.PrintErr("Shotgun: Не вказано WeaponScene у інспекторі!");
		}

		_ammoLabel = GetNodeOrNull<Label>(AmmoLabelPath);

		// Знаходимо Player серед батьківських вузлів.
		Node current = GetParent();

		while (current != null)
		{
			if (current is CharacterBody3D body)
			{
				_player = body;
				break;
			}

			current = current.GetParent();
		}

		_ammoInMagazine = Mathf.Max(0, MagazineCapacity);
		_reserveAmmo = Mathf.Max(0, StartingReserveAmmo);

		_basePosition = Position;
		_rng.Randomize();

		UpdateAmmoUI();
	}

	public override void _Input(InputEvent @event)
	{
		if (_camera == null)
			return;

		// Ліва кнопка миші — постріл.
		if (@event is InputEventMouseButton mouseEvent
			&& mouseEvent.ButtonIndex == MouseButton.Left
			&& mouseEvent.Pressed
			)
		{
			Shoot();
		}

		// R — перезаряджання.
		if (@event is InputEventKey keyEvent
			&& keyEvent.Pressed
			&& !keyEvent.Echo
			&& keyEvent.Keycode == Key.R)
		{
			StartReload();
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_fireTimer > 0.0f)
			_fireTimer -= dt;

		// Поступове перезаряджання по одному патрону.
		if (_isReloading)
		{
			_reloadTimer -= dt;

			if (_reloadTimer <= 0.0f)
			{
				LoadOneShell();
			}
		}

		// Повернення моделі після віддачі.
		_recoilAmount = Mathf.Lerp(
			_recoilAmount,
			0.0f,
			Mathf.Clamp(RecoilRecoverySpeed * dt, 0.0f, 1.0f)
		);

		Position = _basePosition
			+ new Vector3(0.0f, 0.0f, _recoilAmount);

		UpdateAmmoUI();
	}

	private void Shoot()
	{
		
		if (_fireTimer > 0.0f)
			return;

		// Постріл може перервати перезаряджання.
		if (_isReloading)
		{
			_isReloading = false;
			_reloadTimer = 0.0f;
		}

		if (_ammoInMagazine <= 0)
		{
			if (_reserveAmmo > 0)
				StartReload();
			else
				GD.Print("Немає патронів!");
_recoilAmount = Mathf.Abs(RecoilDistance);
			return;
		}

		_ammoInMagazine--;
		_fireTimer = Mathf.Max(0.05f, FireCooldown);
		_recoilAmount = -Mathf.Abs(RecoilDistance);

		// Центр екрана.
		Vector2 screenCenter =
			GetViewport().GetVisibleRect().Size / 2.0f;

		Vector3 origin = _camera.ProjectRayOrigin(screenCenter);
		Vector3 forward = _camera.ProjectRayNormal(screenCenter);

		Vector3 right = _camera.GlobalBasis.X;
		Vector3 up = _camera.GlobalBasis.Y;

		// Один постріл дробовика — декілька променів.
		int pelletCount = Mathf.Max(1, PelletsPerShot);

		for (int i = 0; i < pelletCount; i++)
		{
			float spreadX = _rng.RandfRange(-Spread, Spread);
			float spreadY = _rng.RandfRange(-Spread, Spread);

			Vector3 direction = (
				forward + right * spreadX + up * spreadY
			).Normalized();

			FirePellet(origin, direction);
		}

		GD.Print(
			$"Дробовик: {_ammoInMagazine}/{_reserveAmmo}"
		);

		UpdateAmmoUI();

		// Автоматично починаємо перезаряджання,
		// якщо магазин спорожнів.
		if (_ammoInMagazine == 0 && _reserveAmmo > 0)
			StartReload();
	}

	private void FirePellet(Vector3 origin, Vector3 direction)
	{
		Vector3 end = origin + direction * Range;

		var query = PhysicsRayQueryParameters3D.Create(
			origin,
			end
		);

		query.CollisionMask = CollisionMask;
		query.CollideWithBodies = true;
		query.CollideWithAreas = true;

		// Не дозволяємо променю влучити в самого гравця.
		if (_player != null)
			query.Exclude = new Godot.Collections.Array<Rid>
			{
				_player.GetRid()
			};

		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count == 0)
			return;

		if (!hit.TryGetValue("collider", out Variant colliderValue))
			return;

		GodotObject hitObject = colliderValue.AsGodotObject();
		Node hitNode = hitObject as Node;

		if (hitNode == null)
			return;

		// Шукаємо вузол з методом TakeDamage,
		// навіть якщо влучили в дочірню колізію мутанта.
		Node target = hitNode;

		while (target != null)
		{
			if (target.HasMethod("TakeDamage"))
			{
				target.Call("TakeDamage", DamagePerPellet);
				break;
			}

			target = target.GetParent();
		}
	}

	private void StartReload()
	{
		if (_isReloading)
			return;

		if (_ammoInMagazine >= MagazineCapacity)
			return;

		if (_reserveAmmo <= 0)
			return;

		_isReloading = true;
		_reloadTimer = Mathf.Max(0.05f, ReloadShellTime);

		GD.Print("Перезаряджання дробовика...");
		UpdateAmmoUI();
	}

	private void LoadOneShell()
	{
		if (!_isReloading)
			return;

		if (_ammoInMagazine >= MagazineCapacity || _reserveAmmo <= 0)
		{
			_isReloading = false;
			_reloadTimer = 0.0f;
			UpdateAmmoUI();
			return;
		}

		_ammoInMagazine++;
		_reserveAmmo--;

		UpdateAmmoUI();

		// Якщо магазин ще не повний — заряджаємо наступний.
		if (_ammoInMagazine < MagazineCapacity && _reserveAmmo > 0)
		{
			_reloadTimer = Mathf.Max(0.05f, ReloadShellTime);
		}
		else
		{
			_isReloading = false;
			_reloadTimer = 0.0f;
			GD.Print("Перезаряджання завершено.");
		}
	}

	private void UpdateAmmoUI()
	{
		if (_ammoLabel == null)
			return;

		string status = _isReloading ? " (перезаряджання)" : "";

		_ammoLabel.Text =
			$"ДРОБОВИК  {_ammoInMagazine}/{_reserveAmmo}{status}";
	}

	// Можна викликати з іншого скрипта, щоб додати патрони.
	public void AddReserveAmmo(int amount)
	{
		if (amount <= 0)
			return;

		_reserveAmmo += amount;
		UpdateAmmoUI();
	}
}
