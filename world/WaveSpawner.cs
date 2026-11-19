using Godot;
using System.Collections.Generic;

public partial class WaveSpawner : Node3D
{
	[Export] public PackedScene MutantScene;
	[Export] public int TotalWaves = 4;
	[Export] public int FirstWaveCount = 3;
	[Export] public int ExtraPerWave = 2;
	[Export] public int MaxAlive = 10;
	[Export] public float SpawnInterval = 1.0f;
	[Export] public float FirstWaveDelay = 3.0f;
	[Export] public float TimeBetweenWaves = 5.0f;
	[Export] public float HpGrowthPerWave = 0.15f;
	[Export] public float RandomRadius = 8.0f;

	private readonly List<Marker3D> _points = new();
	private Label _label;
	private int _wave = 0;
	private int _alive = 0;

	public override void _Ready()
	{
		foreach (Node child in GetChildren())
			if (child is Marker3D m)
				_points.Add(m);

		var layer = new CanvasLayer { Layer = 80 };
		AddChild(layer);

		_label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_label.AddThemeFontSizeOverride("font_size", 32);
		_label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_label.OffsetTop = 20;
		layer.AddChild(_label);

		if (MutantScene == null)
		{
			GD.PrintErr("WaveSpawner: не задано MutantScene! Перетягніть mutant.tscn в інспектор.");
			return;
		}

		GD.Print($"WaveSpawner: готово. Точок спавну: {_points.Count}, хвиль: {TotalWaves}");
		RunWaves();
	}

	private async void RunWaves()
	{
		_label.Text = "Приготуйтесь...";
		await Wait(FirstWaveDelay);

		while (_wave < TotalWaves)
		{
			if (!IsInstanceValid(this) || PlayerDead()) return;

			_wave++;
			int count = FirstWaveCount + (_wave - 1) * ExtraPerWave;
			_label.Text = $"Хвиля {_wave}/{TotalWaves}";
			GD.Print($"Починається хвиля {_wave}, мутантів: {count}");

			int spawned = 0;
			while (spawned < count)
			{
				if (!IsInstanceValid(this) || PlayerDead()) return;

				if (_alive < MaxAlive)
				{
					SpawnMutant();
					spawned++;
				}
				await Wait(SpawnInterval);
			}

			while (IsInstanceValid(this) && _alive > 0 && !PlayerDead())
				await Wait(0.5f);

			if (!IsInstanceValid(this) || PlayerDead()) return;

			if (_wave < TotalWaves)
			{
				_label.Text = $"Хвилю {_wave} пройдено!";
				await Wait(TimeBetweenWaves);
			}
		}

		_label.Text = "ПЕРЕМОГА! Усі хвилі пройдено";
		GD.Print("Усі хвилі пройдено!");
	}

	private void SpawnMutant()
	{
		var node = MutantScene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(node);
		node.GlobalPosition = GetSpawnPosition();

		if (node is Mutant mutant)
			mutant.hp *= 1.0f + HpGrowthPerWave * (_wave - 1);
		else
			GD.PrintErr("У корені mutant.tscn немає скрипта Mutant.cs!");

		GD.Print("Мутант створений у ", node.GlobalPosition);

		_alive++;
		node.TreeExited += () =>
		{
			if (IsInstanceValid(this))
				_alive--;
		};
	}

	private Vector3 GetSpawnPosition()
	{
		Vector3 pos;

		if (_points.Count > 0)
		{
			pos = _points[(int)(GD.Randi() % _points.Count)].GlobalPosition;
		}
		else
		{
			float angle = GD.Randf() * Mathf.Tau;
			float dist = GD.Randf() * RandomRadius;
			pos = GlobalPosition + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
		}

		pos += new Vector3(GD.Randf() * 2f - 1f, 0.2f, GD.Randf() * 2f - 1f);
		return pos;
	}

	private bool PlayerDead()
	{
		var p = GetTree().GetFirstNodeInGroup("player") as Player;
		return p != null && p.IsDead;
	}

	private async System.Threading.Tasks.Task Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}
}
