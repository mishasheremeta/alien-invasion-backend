using Godot;

public partial class Mutant : CharacterBody3D
{
	[Export]
	public NodePath PlayerPath { get; set; }

	private Node3D player = null;
	private float hp = 15.0f;

	private const float Speed = 4.0f;
	private const float AttackRange = 2.0f;
	private const float Damage = 2.0f;

	private NavigationAgent3D navAgent;
	private AnimationPlayer animPlayer;

	public override void _Ready()
	{
		navAgent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");

		animPlayer = GetNodeOrNull<AnimationPlayer>("combo attack/Skeleton3D/AnimationPlayer");
		if (animPlayer == null) animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		if (animPlayer != null)
		{
			SetupAnimation("run");
			SetupAnimation("walking");
			SetupAnimation("combo attack");
		}

		if (PlayerPath != null && !PlayerPath.IsEmpty)
		{
			player = GetNodeOrNull<Node3D>(PlayerPath);
		}

		Callable.From(ActorSetup).CallDeferred();
	}

	private void SetupAnimation(string animName)
	{
		if (!animPlayer.HasAnimation(animName)) return;

		var anim = animPlayer.GetAnimation(animName);
		anim.LoopMode = Animation.LoopModeEnum.Linear;

		// Принудительно отключаем дорожки перемещения кости Hips в коде
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string trackPath = anim.TrackGetPath(i).ToString();
			if (trackPath.Contains("Hips") && anim.TrackGetType(i) == Animation.TrackType.Position3D)
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
			navAgent.TargetPosition = player.GlobalPosition;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (player == null) return;

		SafeLookAt(player.GlobalPosition);

		if (TargetInRange())
		{
			Velocity = Vector3.Zero;

			if (animPlayer != null && (!animPlayer.IsPlaying() || animPlayer.CurrentAnimation != "combo attack"))
			{
				animPlayer.Play("combo attack");
			}
		}
		else
		{
			if (animPlayer != null && (!animPlayer.IsPlaying() || animPlayer.CurrentAnimation != "run"))
			{
				animPlayer.Play("run");
			}

			if (navAgent != null)
			{
				navAgent.TargetPosition = player.GlobalPosition;
				Vector3 nextNavPoint = navAgent.GetNextPathPosition();
				Vector3 direction = (nextNavPoint - GlobalPosition).Normalized();
				Velocity = new Vector3(direction.X * Speed, Velocity.Y, direction.Z * Speed);
			}
		}

		MoveAndSlide();
	}

	public void ComboAttackPlayer()
	{
		if (TargetInRange() && player != null)
		{
			Vector3 dir = GlobalPosition.DirectionTo(player.GlobalPosition);
			player.Call("comboattack", Damage, dir);
		}
	}

	public bool TargetInRange()
	{
		if (player == null) return false;
		return GlobalPosition.DistanceTo(player.GlobalPosition) <= AttackRange;
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
