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
	// АУДІОПЛЕЄРИ ТА ФАЙЛИ ЗВУКІВ
	// ==========================================
	private AudioStreamPlayer _jumpPlayer;
	private AudioStreamPlayer _runPlayer;
	private AudioStreamPlayer _movePlayer;

	private readonly AudioStream _walkSound = GD.Load<AudioStream>("res://ходьба.wav");
	private readonly AudioStream _sprintSound = GD.Load<AudioStream>("res://біг.wav");
	private readonly AudioStream _jumpSound = GD.Load<AudioStream>("res://прижок.wav");

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
		_baseCamPos = _camera.Position;

		Input.MouseMode = Input.MouseModeEnum.Captured;

		_jumpPlayer = GetNodeOrNull<AudioStreamPlayer>("jump");
		_runPlayer = GetNodeOrNull<AudioStreamPlayer>("run");
		_movePlayer = GetNodeOrNull<AudioStreamPlayer>("move");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouse)
		{
			RotateY(-mouse.Relative.X * MouseSensitivity);

			_camRotX = Mathf.Clamp(_camRotX - mouse.Relative.Y * MouseSensitivity, -Mathf.DegToRad(89), Mathf.DegToRad(89));
			_camera.Rotation = new Vector3(_camRotX, 0, 0);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
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
			(Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0),
			(Input.IsKeyPressed(Key.S) ? 1 : 0) - (Input.IsKeyPressed(Key.W) ? 1 : 0)
		);
		input = input.Normalized();

		Vector3 dir = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();

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
			newCamPos.X += Mathf.Cos(_bobTimer * 0.5f) * (BobAmplitude * 0.5f) * speedMultiplier;

			_camera.Position = newCamPos;

			CheckFootstepSound(isSprinting);
		}
		else
		{
			_bobTimer = 0.0f;
			_camera.Position = _camera.Position.Lerp(_baseCamPos, delta * 10.0f);
		}
	}

	private void CheckFootstepSound(bool isSprinting)
	{
		if (_bobTimer >= _nextFootstepDistance)
		{
			if (isSprinting)
			{
				if (_runPlayer != null)
				{
					if (_runPlayer.Stream == null && _sprintSound != null)
						_runPlayer.Stream = _sprintSound;

					if (!_runPlayer.Playing)
						_runPlayer.Play();
				}
			}
			else
			{
				if (_movePlayer != null)
				{
					if (_movePlayer.Stream == null && _walkSound != null)
						_movePlayer.Stream = _walkSound;

					if (!_movePlayer.Playing)
						_movePlayer.Play();
				}
			}

			_nextFootstepDistance = _bobTimer + Mathf.Pi;
		}
	}

	private void PlayJumpSound()
	{
		if (_jumpPlayer != null)
		{
			if (_jumpPlayer.Stream == null && _jumpSound != null)
				_jumpPlayer.Stream = _jumpSound;

			_jumpPlayer.Play();
		}
	}
}
