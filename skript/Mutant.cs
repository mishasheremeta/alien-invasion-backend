using Godot;

public partial class Mutant : CharacterBody3D
{
	[Export] public NodePath PlayerPath { get; set; }
	[Export] public float AttackRange { get; set; } = 2.5f;
	[Export] public float Damage { get; set; } = 20.0f;
	[Export] public float AttackCooldown { get; set; } = 0.8f;
	[Export(PropertyHint.Range, "0,1,0.05")]
	public float HitMoment { get; set; } = 0.45f;
	[Export] public float AttackAnimSpeed { get; set; } = 1.0f;

	private const string AttackAnim = "combo attack";
	private const string RunAnim = "run";
	private const string IdleAnim = "idle";

	public float hp = 40.0f;
	private const float Speed = 4.0f;

	private Player player = null;

	private float cooldownTimer = 0.0f;
	private bool isAttacking = false;
	private bool hitDone = false;
	private float attackElapsed = 0.0f;
	private float attackLength = 1.0f;

	private NavigationAgent3D navAgent;
	private AnimationPlayer animPlayer;

	public override void _Ready()
	{
		navAgent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");

		animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animPlayer == null)
			animPlayer = GetNodeOrNull<AnimationPlayer>("combo attack/Skeleton3D/AnimationPlayer");

		var tree = GetNodeOrNull<AnimationTree>("AnimationTree");
		if (tree != null)
			tree.Active = false;

		if (animPlayer != null)
		{
			SetupAnimation(RunAnim, true);
			SetupAnimation("walking", true);
			SetupAnimation(IdleAnim, true);
			SetupAnimation(AttackAnim, false);

			if (animPlayer.HasAnimation(AttackAnim))
				attackLength = (float)animPlayer.GetAnimation(AttackAnim).Length / AttackAnimSpeed;
			else
				GD.PrintErr($"Анімацію '{AttackAnim}' не знайдено. Є: ",
					string.Join(", ", animPlayer.GetAnimationList()));
		}
		else
		{
			GD.PrintErr("Мутант: AnimationPlayer не знайдено!");
		}

		FindPlayer();
		Callable.From(ActorSetup).CallDeferred();
	}

	private void FindPlayer()
	{
		if (PlayerPath != null && !PlayerPath.IsEmpty)
			player = GetNodeOrNull<Player>(PlayerPath);

		if (player == null)
			player = GetTree().GetFirstNodeInGroup("player") as Player;
	}

	public void TakeDamage(float amount)
	{
		hp -= amount;
		GD.Print($"Мутант отримав {amount} шкоди. HP: {hp}");

		if (hp <= 0.0f)
		{
			GD.Print("Мутанта знищено!");
			QueueFree();
		}
	}

	private void SetupAnimation(string animName, bool loop)
	{
		if (animPlayer == null || !animPlayer.HasAnimation(animName))
			return;

		var anim = animPlayer.GetAnimation(animName);
		anim.LoopMode = loop
			? Animation.LoopModeEnum.Linear
			: Animation.LoopModeEnum.None;

		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string trackPath = anim.TrackGetPath(i).ToString();

			if (trackPath.Contains("Hips") &&
				anim.TrackGetType(i) == Animation.TrackType.Position3D)
			{
				anim.TrackSetEnabled(i, false);
			}
		}
	}

	private async void ActorSetup()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		if (player != null && navAgent != null)
		{
			navAgent.TargetDesiredDistance = 0.5f;
			navAgent.TargetPosition = player.GlobalPosition;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float fDelta = (float)delta;
		Vector3 vel = Velocity;

		// Гравітація працює завжди, навіть якщо гравця немає
		if (!IsOnFloor())
			vel += GetGravity() * fDelta;

		if (player == null || !GodotObject.IsInstanceValid(player))
		{
			FindPlayer();
			if (player == null)
			{
				vel.X = 0;
				vel.Z = 0;
				Velocity = vel;
				MoveAndSlide();
				return;
			}
		}

		// Гравець мертвий: зупиняємось
		if (player.IsDead)
		{
			isAttacking = false;
			vel.X = 0;
			vel.Z = 0;
			Velocity = vel;
			PlayLoop(IdleAnim);
			MoveAndSlide();
			return;
		}

		cooldownTimer -= fDelta;
		SafeLookAt(player.GlobalPosition);

		bool inRange = TargetInRange(AttackRange);

		if (isAttacking || inRange)
		{
			vel.X = 0;
			vel.Z = 0;

			if (!isAttacking && cooldownTimer <= 0.0f)
				StartAttack();

			if (isAttacking)
				UpdateAttack(fDelta);
			else
				PlayLoop(IdleAnim);
		}
		else
		{
			PlayLoop(RunAnim);

			Vector3 targetPoint = player.GlobalPosition;

			if (navAgent != null)
			{
				navAgent.TargetPosition = player.GlobalPosition;

				if (!navAgent.IsNavigationFinished())
					targetPoint = navAgent.GetNextPathPosition();
			}

			Vector3 direction = targetPoint - GlobalPosition;
			direction.Y = 0;
			direction = direction.Normalized();

			vel.X = direction.X * Speed;
			vel.Z = direction.Z * Speed;
		}

		Velocity = vel;
		MoveAndSlide();
	}

	private void StartAttack()
	{
		isAttacking = true;
		hitDone = false;
		attackElapsed = 0.0f;

		if (animPlayer != null && animPlayer.HasAnimation(AttackAnim))
		{
			animPlayer.SpeedScale = AttackAnimSpeed;
			animPlayer.Play(AttackAnim, 0.1);
			animPlayer.Seek(0.0, true);
		}
	}

	private void UpdateAttack(float delta)
	{
		attackElapsed += delta;

		if (!hitDone && attackElapsed >= attackLength * HitMoment)
		{
			hitDone = true;

			if (TargetInRange(AttackRange + 0.7f))
			{
				player.TakeDamage(Damage);
				GD.Print("Мутант вдарив гравця!");
			}
		}

		if (attackElapsed >= attackLength)
		{
			isAttacking = false;
			cooldownTimer = AttackCooldown;

			if (animPlayer != null)
				animPlayer.SpeedScale = 1.0f;
		}
	}

	private void PlayLoop(string name)
	{
		if (animPlayer == null || !animPlayer.HasAnimation(name))
			return;

		animPlayer.SpeedScale = 1.0f;

		if (!animPlayer.IsPlaying() || animPlayer.CurrentAnimation != name)
			animPlayer.Play(name, 0.15);
	}

	public bool TargetInRange(float range)
	{
		if (player == null)
			return false;

		Vector3 a = GlobalPosition;
		Vector3 b = player.GlobalPosition;

		return new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z)) <= range;
	}

	private void SafeLookAt(Vector3 targetPosition)
	{
		Vector3 lookTarget = new Vector3(targetPosition.X, GlobalPosition.Y, targetPosition.Z);

		if (!GlobalPosition.IsEqualApprox(lookTarget))
		{
			LookAt(lookTarget, Vector3.Up);
			RotateY(Mathf.Pi);
		}
	}
}
