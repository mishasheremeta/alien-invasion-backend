using Godot;

public partial class Player : CharacterBody3D
{
	// Налаштування швидкості руху та стрибка
	public const float Speed = 5.0f;
	public const float SprintSpeed = 9.0f;
	public const float JumpVelocity = 4.5f;
	[ExportGroup("Head Bobbing")]
	[Export] public float BobFrequency = 2.4f;        // Частота кроків (швидкість коливань)
	[Export] public float BobAmplitude = 0.08f;       // Висота та ширина амплітуди
	[Export] public float BobSprintMultiplier = 1.4f; // Наскільки сильніше погойдування при бігу

	private Vector3 _baseCamPos; // Зберігає початкову позицію камери в сцені
	private float _bobTimer = 0.0f; // Лічильник для розрахунку хвилі (синусоїди)


	// Параметри плавності проковзування та чутливості миші (доступні в Інспекторі)
	[Export] public float GroundInertia = 12.0f; // Інерція на землі (менше значення = довше ковзання)
	[Export] public float AirInertia = 3.0f;     // Інерція у повітрі під час стрибка
	[Export] public float MouseSensitivity = 0.002f;

	private Camera3D _camera;
	private float _camRotX; // Накопичувач кута нахилу камери по вертикалі

	public override void _Ready()
	{
		// Отримуємо посилання на вузол камери та ховаємо курсор миші
		_camera = GetNode<Camera3D>("Camera3D");
		_baseCamPos = _camera.Position;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Обробка руху миші
		if (@event is InputEventMouseMotion mouse)
		{
			// Поворот тіла гравця ліворуч/праворуч (вісь Y)
			RotateY(-mouse.Relative.X * MouseSensitivity);

			// Розрахунок та обмеження нахилу камери вгору/вниз (вісь X) до 89 градусів
			_camRotX = Mathf.Clamp(_camRotX - mouse.Relative.Y * MouseSensitivity, -Mathf.DegToRad(89), Mathf.DegToRad(89));
			_camera.Rotation = new Vector3(_camRotX, 0, 0);
		}
	}

	// ✅ ЯК МЕТОД МАЄ ВИГЛЯДАТИ:
	public override void _PhysicsProcess(double delta)
	{
		Vector3 vel = Velocity;
		float fDelta = (float)delta;

		if (!IsOnFloor()) vel += GetGravity() * fDelta;

		if (Input.IsKeyPressed(Key.Space) && IsOnFloor()) vel.Y = JumpVelocity;

		// 1. Правильно закриваємо Vector2 крапкою з комою
		Vector2 input = new(
			(Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0),
			(Input.IsKeyPressed(Key.S) ? 1 : 0) - (Input.IsKeyPressed(Key.W) ? 1 : 0)
		);
		input = input.Normalized();

		Vector3 dir = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();

		// 2. Створюємо змінну isSprinting
		bool isSprinting = Input.IsKeyPressed(Key.Shift);
		float targetSpeed = isSprinting ? SprintSpeed : Speed;
		float lerp = (IsOnFloor() ? GroundInertia : AirInertia) * fDelta;

		Vector3 targetVel = dir * targetSpeed;
		vel.X = Mathf.Lerp(vel.X, targetVel.X, lerp);
		vel.Z = Mathf.Lerp(vel.Z, targetVel.Z, lerp);

		// 3. Застосовуємо рух
		Velocity = vel;
		MoveAndSlide();

		// 4. І ЛИШЕ В КІНЦІ викликаємо погойдування камери
		HandleHeadBob(fDelta, isSprinting);
	}
	private void HandleHeadBob(float delta, bool isSprinting)
	{
		// Рахуємо швидкість лише в горизонтальній площині (ігноруємо стрибки/падіння)
		Vector3 horizontalVel = new Vector3(Velocity.X, 0, Velocity.Z);
		float speed = horizontalVel.Length();

		// Погойдування працює ЛИШЕ на землі і якщо гравець реально рухається
		if (IsOnFloor() && speed > 0.1f)
		{
			float speedMultiplier = isSprinting ? BobSprintMultiplier : 1.0f;
			_bobTimer += delta * speed * speedMultiplier * BobFrequency;

			// Формуємо нову позицію (Y — вгору/вниз, X — вліво/вправо у 2 рази повільніше)
			Vector3 newCamPos = _baseCamPos;
			newCamPos.Y += Mathf.Sin(_bobTimer) * BobAmplitude * speedMultiplier;
			newCamPos.X += Mathf.Cos(_bobTimer * 0.5f) * (BobAmplitude * 0.5f) * speedMultiplier;

			_camera.Position = newCamPos;
		}
		else
		{
			// Якщо зупинилися або в повітрі — скидаємо таймер і плавно повертаємо камеру назад
			_bobTimer = 0.0f;
			_camera.Position = _camera.Position.Lerp(_baseCamPos, delta * 10.0f);
		}
	}
}
