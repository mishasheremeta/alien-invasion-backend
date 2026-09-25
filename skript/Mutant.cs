using Godot;

public partial class Mutant : CharacterBody3D
{
	private Node3D player = null;
	private float hp = 15.0f;
	private AnimationNodeStateMachinePlayback stateMachine;

	private const float Speed = 4.0f;
	private const float AttackRange = 2.0f;
	private const float Damage = 2.0f;

	[Export]
	public NodePath PlayerPath { get; set; }

	private NavigationAgent3D navAgent;
	private AnimationTree animTree;
	private CollisionShape3D collisionShape;

	public override void _Ready()
	{
		navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		animTree = GetNode<AnimationTree>("AnimationTree");
		collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");

		if (PlayerPath != null)
		{
			player = GetNode<Node3D>(PlayerPath);
		}

		stateMachine = (AnimationNodeStateMachinePlayback)animTree.Get("parameters/playback");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (player == null) return;

		string currentNode = stateMachine.GetCurrentNode();

		switch (currentNode)
		{
			case "walking":
				animTree.Set("parameters/conditions/run", true);
				break;

			case "run":
				Velocity = Vector3.Zero;
				break;

			case "shock":
				animTree.Set("parameters/conditions/run", !TargetInRange());
				SafeLookAt(player.GlobalPosition);
				break;

			case "death":
				break;

			case "combo attack":
				break;
		}

		// Переміщення та слідування за гравцем
		navAgent.TargetPosition = player.GlobalPosition;
		Vector3 nextNavPoint = navAgent.GetNextPathPosition();

		Velocity = (nextNavPoint - GlobalPosition).Normalized() * Speed;
		SafeLookAt(player.GlobalPosition);

		animTree.Set("parameters/conditions/shock", TargetInRange());

		MoveAndSlide();
	}

	public void ComboAttackPlayer()
	{
		if (TargetInRange())
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

	// Безпечний поворот, щоб уникнути помилки LookAt (коли позиція ворога і гравця збігаються)
	private void SafeLookAt(Vector3 targetPosition)
	{
		Vector3 lookTarget = new Vector3(targetPosition.X, GlobalPosition.Y, targetPosition.Z);
		if (!GlobalPosition.IsEqualApprox(lookTarget))
		{
			LookAt(lookTarget, Vector3.Up);
		}
	}
}
