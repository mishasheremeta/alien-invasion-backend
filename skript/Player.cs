using Godot;

public partial class Player : CharacterBody3D
{
	// ==========================================
	// НАЛАШТУВАННЯ ПЕРСОНАЖА ТА РУХУ
	// ==========================================
	public const float Speed = 5.0f;
	public const float SprintSpeed = 9.0f;
	public const float JumpVelocity = 4.5f;

	[ExportGroup("Head Bobbing")]
	[Export] public float BobFrequency = 1.8f;
	[Export] public float BobAmplitude = 0.05f;
	[Export] public float BobSprintMultiplier = 0.8f;

	private Vector3 _baseCamPos;
	private float _bobTimer = 0.0f;
	private float _nextFootstepDistance = 0.0f;

	[Export] public float GroundInertia = 12.0f;
	[Export] public float AirInertia = 3.0f;
	[Export] public float MouseSensitivity = 0.002f;

	private Camera3D _camera;
	private float _camRotX;

	// ==========================================
	// ЗДОРОВ'Я ГРАВЦЯ
	// ==========================================
	[ExportGroup("Health")]
	[Export] public float MaxHealth = 100.0f;

	[ExportGroup("Death")]
	[Export] public float RestartDelay = 3.0f;

	private float _health;
	private bool _isDead = false;

	public float Health => _health;
	public bool IsDead => _isDead;

	// ==========================================
	// АУДІОПЛЕЄРИ ТА ФАЙЛИ ЗВУКІВ
	// ==========================================
	private AudioStreamPlayer3D _jumpPlayer;
	private AudioStreamPlayer3D _runPlayer;
	private AudioStreamPlayer3D _movePlayer;

	private AudioStream _walkSound;
	private AudioStream _sprintSound;
	private AudioStream _jumpSound;

	// ==========================================
	// ІНІЦІАЛІЗАЦІЯ
	// ==========================================
	public override void _Ready()
	{
		_health = MaxHealth;

		_camera = GetNode<Camera3D>("Camera3D");
		_baseCamPos = _camera.Position;

		Input.MouseMode = Input.MouseModeEnum.Captured;

		_jumpPlayer = GetNodeOrNull<AudioStreamPlayer3D>("jump");
		_runPlayer = GetNodeOrNull<AudioStreamPlayer3D>("run");
		_movePlayer = GetNodeOrNull<AudioStreamPlayer3D>("move");

		_walkSound = GD.Load<AudioStream>("res://sounds/ходьба.wav");
		_sprintSound = GD.Load<AudioStream>("res://sounds/біг.wav");
		_jumpSound = GD.Load<AudioStream>("res://sounds/прижок.wav");

		if (_jumpPlayer == null) GD.PrintErr("Вузол 'jump' не знайдено");
		if (_runPlayer == null) GD.PrintErr("Вузол 'run' не знайдено");
		if (_movePlayer == null) GD.PrintErr("Вузол 'move' не знайдено");
		if (_walkSound == null) GD.PrintErr("Звук ходьби не завантажено");
		if (_sprintSound == null) GD.PrintErr("Звук бігу не завантажено");
		if (_jumpSound == null) GD.PrintErr("Звук стрибка не завантажено");

		GD.Print("Здоров'я гравця: ", _health);
	}

	// ==========================================
	// КЕРУВАННЯ МИШКОЮ
	// ==========================================
	public override void _UnhandledInput(InputEvent @event)
	{
		if (_isDead)
			return;

		if (@event is InputEventMouseMotion mouse)
		{
			RotateY(-mouse.Relative.X * MouseSensitivity);

			_camRotX = Mathf.Clamp(
				_camRotX - mouse.Relative.Y * MouseSensitivity,
				-Mathf.DegToRad(89),
				Mathf.DegToRad(89)
			);

			_camera.Rotation = new Vector3(_camRotX, 0, 0);
		}

		if (@event is InputEventKey key &&
			key.Pressed &&
			key.Keycode == Key.Escape)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	// ==========================================
	// ФІЗИКА ТА РУХ
	// ==========================================
	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		Vector3 vel = Velocity;
		float fDelta = (float)delta;

		if (!IsOnFloor())
			vel += GetGravity() * fDelta;

		if (Input.IsKeyPressed(Key.Space) && IsOnFloor())
		{
			vel.Y = JumpVelocity;
			PlayJumpSound();
		}

		Vector2 input = new(
			(Input.IsKeyPressed(Key.D) ? 1 : 0) -
			(Input.IsKeyPressed(Key.A) ? 1 : 0),

			(Input.IsKeyPressed(Key.S) ? 1 : 0) -
			(Input.IsKeyPressed(Key.W) ? 1 : 0)
		);

		input = input.Normalized();

		Vector3 dir = (
			Transform.Basis * new Vector3(input.X, 0, input.Y)
		).Normalized();

		bool isSprinting = Input.IsKeyPressed(Key.Shift);
		float targetSpeed = isSprinting ? SprintSpeed : Speed;

		float lerp = (IsOnFloor() ? GroundInertia : AirInertia) * fDelta;

		Vector3 targetVel = dir * targetSpeed;

		vel.X = Mathf.Lerp(vel.X, targetVel.X, lerp);
		vel.Z = Mathf.Lerp(vel.Z, targetVel.Z, lerp);

		Velocity = vel;

		MoveAndSlide();

		HandleHeadBob(fDelta, isSprinting);
	}

	// ==========================================
	// ПОХИТУВАННЯ КАМЕРИ
	// ==========================================
	private void HandleHeadBob(float delta, bool isSprinting)
	{
		Vector3 horizontalVel = new Vector3(Velocity.X, 0, Velocity.Z);
		float speed = horizontalVel.Length();

		if (IsOnFloor() && speed > 0.1f)
		{
			float speedMultiplier = isSprinting ? BobSprintMultiplier : 1.0f;

			_bobTimer += delta * speed * speedMultiplier * BobFrequency;

			Vector3 newCamPos = _baseCamPos;

			newCamPos.Y += Mathf.Sin(_bobTimer) * BobAmplitude * speedMultiplier;
			newCamPos.X += Mathf.Cos(_bobTimer * 0.5f) *
				(BobAmplitude * 0.5f) * speedMultiplier;

			_camera.Position = newCamPos;

			CheckFootstepSound(isSprinting);
		}
		else
		{
			_bobTimer = 0.0f;
			_nextFootstepDistance = 0.0f;

			_camera.Position = _camera.Position.Lerp(_baseCamPos, delta * 10.0f);

			StopPlayer(_runPlayer);
			StopPlayer(_movePlayer);
		}
	}

	// ==========================================
	// ЗВУКИ КРОКІВ
	// ==========================================
	private void CheckFootstepSound(bool isSprinting)
	{
		if (isSprinting)
			StopPlayer(_movePlayer);
		else
			StopPlayer(_runPlayer);

		if (_bobTimer >= _nextFootstepDistance)
		{
			AudioStreamPlayer3D player = isSprinting ? _runPlayer : _movePlayer;
			AudioStream sound = isSprinting ? _sprintSound : _walkSound;

			if (player != null)
			{
				if (player.Stream == null && sound != null)
					player.Stream = sound;

				if (!player.Playing)
					player.Play();
			}

			_nextFootstepDistance = _bobTimer + Mathf.Pi;
		}
	}

	// ==========================================
	// ЗВУК СТРИБКА
	// ==========================================
	private void PlayJumpSound()
	{
		if (_jumpPlayer != null)
		{
			if (_jumpPlayer.Stream == null && _jumpSound != null)
				_jumpPlayer.Stream = _jumpSound;

			_jumpPlayer.Play();
		}
	}

	private static void StopPlayer(AudioStreamPlayer3D player)
	{
		if (player != null && player.Playing)
			player.Stop();
	}

	// ==========================================
	// ОТРИМАННЯ УРОНУ ВІД ПРОТИВНИКА
	// ==========================================
	public void TakeDamage(float damage)
	{
		if (_isDead || damage <= 0)
			return;

		_health -= damage;
		_health = Mathf.Max(_health, 0);

		GD.Print("Гравець отримав урон: ", damage);
		GD.Print("Здоров'я: ", _health, "/", MaxHealth);

		if (_health <= 0)
			Die();
	}

	// ==========================================
	// ВІДНОВЛЕННЯ ЗДОРОВ'Я
	// ==========================================
	public void Heal(float amount)
	{
		if (_isDead || amount <= 0)
			return;

		_health = Mathf.Min(_health + amount, MaxHealth);

		GD.Print("Здоров'я відновлено: ", _health);
	}

	// ==========================================
	// СМЕРТЬ ГРАВЦЯ + АНІМАЦІЯ
	// ==========================================
	private void Die()
	{
		if (_isDead)
			return;

		_isDead = true;

		GD.Print("ГРАВЕЦЬ ПЕРЕМОЖЕНИЙ");

		Velocity = Vector3.Zero;

		StopPlayer(_runPlayer);
		StopPlayer(_movePlayer);

		SetPhysicsProcess(false);
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Ховаємо зброю
		var weapon = GetNodeOrNull<Node3D>("Camera3D/WeaponHolder");
		if (weapon != null)
			weapon.Visible = false;

		// Падіння камери
		var camTween = CreateTween().SetParallel(true);

		camTween.TweenProperty(_camera, "position",
				new Vector3(_baseCamPos.X, _baseCamPos.Y - 1.2f, _baseCamPos.Z), 0.8f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);

		camTween.TweenProperty(_camera, "rotation_degrees",
				new Vector3(-20.0f, 0.0f, 75.0f), 0.8f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);

		// Червоний екран + напис
		var layer = new CanvasLayer { Layer = 100 };
		AddChild(layer);

		var red = new ColorRect
		{
			Color = new Color(0.6f, 0f, 0f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		red.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(red);

		var label = new Label
		{
			Text = "ВИ ЗАГИНУЛИ",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Modulate = new Color(1, 1, 1, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 64);
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(label);

		var uiTween = CreateTween().SetParallel(true);
		uiTween.TweenProperty(red, "color:a", 0.6f, 1.5f);
		uiTween.TweenProperty(label, "modulate:a", 1.0f, 1.5f);

		// Перезапуск рівня
		GetTree().CreateTimer(RestartDelay).Timeout += () =>
			GetTree().ReloadCurrentScene();
	}

	// ==========================================
	// ОТРИМАННЯ ПОТОЧНОГО ЗДОРОВ'Я
	// ==========================================
	public float GetHealth()
	{
		return _health;
	}
}
