using Godot;

public partial class Menu : Control
{
	[Export(PropertyHint.File, "*.tscn")]
	public string GameScenePath { get; set; } = "res://world/map.tscn";

	private static readonly Vector2I[] Resolutions =
	{
		new(1280, 720), new(1366, 768), new(1600, 900),
		new(1920, 1080), new(2560, 1440)
	};

	private static readonly int[] FpsLimits = { 30, 60, 120, 144, 0 }; // 0 = без обмеження

	private const string SettingsPath = "user://settings.cfg";

	private Panel _panel;
	private Button _start, _settings, _quit, _back;

	private OptionButton _resOption, _fpsOption;
	private CheckBox _fullscreenBox, _vsyncBox, _fpsCounterBox;

	private int _resIndex = 3;
	private int _fpsIndex = 1;
	private bool _fullscreen = false;
	private bool _vsync = false;
	private bool _showFps = false;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;

		_panel = GetNode<Panel>("Panel");
		_start = GetNode<Button>("Button");
		_settings = GetNode<Button>("Button2");
		_quit = GetNode<Button>("Button3");
		_back = GetNode<Button>("Panel/Label/Button3");

		// Сигнали підключаємо з коду
		_start.Pressed += OnStartPressed;
		_settings.Pressed += OnSettingPressed;
		_quit.Pressed += OnQuitPressed;
		_back.Pressed += OnBackPressed;

		ShowMainMenu();

		LoadSettings();
		BuildSettingsUI();
		ApplySettings();
	}

	// ==========================================
	// КНОПКИ МЕНЮ
	// ==========================================
	private void ShowMainMenu()
	{
		_start.Visible = true;
		_settings.Visible = true;
		_quit.Visible = true;
		_panel.Visible = false;
	}

	private void OnStartPressed()
	{
		SaveSettings();
		Error err = GetTree().ChangeSceneToFile(GameScenePath);
		if (err != Error.Ok)
			GD.PrintErr($"Не вдалося завантажити сцену: {GameScenePath} ({err})");
	}

	private void OnSettingPressed()
	{
		_start.Visible = false;
		_settings.Visible = false;
		_quit.Visible = false;
		_panel.Visible = true;
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}

	private void OnBackPressed()
	{
		SaveSettings();
		ShowMainMenu();
	}

	// ==========================================
	// ІНТЕРФЕЙС НАЛАШТУВАНЬ
	// ==========================================
	private void BuildSettingsUI()
	{
		var box = new VBoxContainer();
		box.SetAnchorsPreset(Control.LayoutPreset.Center);
		box.OffsetLeft = -200;
		box.OffsetRight = 200;
		box.OffsetTop = -90;
		box.OffsetBottom = 110;
		box.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(box);

		_resOption = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
		foreach (var r in Resolutions)
			_resOption.AddItem($"{r.X} x {r.Y}");
		_resOption.Selected = _resIndex;
		_resOption.ItemSelected += i => { _resIndex = (int)i; ApplySettings(); SaveSettings(); };
		box.AddChild(MakeRow("Роздільна здатність", _resOption));

		_fullscreenBox = new CheckBox { ButtonPressed = _fullscreen };
		_fullscreenBox.Toggled += on => { _fullscreen = on; ApplySettings(); SaveSettings(); };
		box.AddChild(MakeRow("Повний екран", _fullscreenBox));

		_vsyncBox = new CheckBox { ButtonPressed = _vsync };
		_vsyncBox.Toggled += on => { _vsync = on; ApplySettings(); SaveSettings(); };
		box.AddChild(MakeRow("VSync", _vsyncBox));

		_fpsOption = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
		foreach (int f in FpsLimits)
			_fpsOption.AddItem(f == 0 ? "Без обмеження" : $"{f} FPS");
		_fpsOption.Selected = _fpsIndex;
		_fpsOption.ItemSelected += i => { _fpsIndex = (int)i; ApplySettings(); SaveSettings(); };
		box.AddChild(MakeRow("Ліміт FPS", _fpsOption));

		_fpsCounterBox = new CheckBox { ButtonPressed = _showFps };
		_fpsCounterBox.Toggled += on => { _showFps = on; ApplySettings(); SaveSettings(); };
		box.AddChild(MakeRow("Показувати FPS", _fpsCounterBox));
	}

	private static HBoxContainer MakeRow(string text, Control control)
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		row.AddChild(control);
		return row;
	}

	// ==========================================
	// ЗАСТОСУВАННЯ
	// ==========================================
	private void ApplySettings()
	{
		if (_fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);

			Vector2I size = Resolutions[_resIndex];
			DisplayServer.WindowSetSize(size);

			int screen = DisplayServer.WindowGetCurrentScreen();
			Vector2I pos = DisplayServer.ScreenGetPosition(screen)
				+ (DisplayServer.ScreenGetSize(screen) - size) / 2;
			DisplayServer.WindowSetPosition(pos);
		}

		DisplayServer.WindowSetVsyncMode(
			_vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
		Engine.MaxFps = FpsLimits[_fpsIndex];

		UpdateFpsCounter();
	}

	// Лічильник живе в корені дерева, тому лишається і в грі
	private void UpdateFpsCounter()
	{
		Node root = GetTree().Root;
		var existing = root.GetNodeOrNull<CanvasLayer>("FpsCounter");

		if (!_showFps)
		{
			existing?.QueueFree();
			return;
		}

		if (existing != null)
			return;

		var layer = new CanvasLayer { Name = "FpsCounter", Layer = 120 };
		var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 5);
		label.Position = new Vector2(12, 8);
		layer.AddChild(label);

		var timer = new Timer { WaitTime = 0.25, Autostart = true };
		timer.Timeout += () => label.Text = $"FPS: {Engine.GetFramesPerSecond()}";
		layer.AddChild(timer);

		root.AddChild(layer);
	}

	// ==========================================
	// ЗБЕРЕЖЕННЯ
	// ==========================================
	private void SaveSettings()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("video", "resolution", _resIndex);
		cfg.SetValue("video", "fullscreen", _fullscreen);
		cfg.SetValue("video", "vsync", _vsync);
		cfg.SetValue("video", "fps_limit", _fpsIndex);
		cfg.SetValue("video", "show_fps", _showFps);
		cfg.Save(SettingsPath);
	}

	private void LoadSettings()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SettingsPath) != Error.Ok)
			return;

		_resIndex = Mathf.Clamp((int)cfg.GetValue("video", "resolution", _resIndex), 0, Resolutions.Length - 1);
		_fullscreen = (bool)cfg.GetValue("video", "fullscreen", _fullscreen);
		_vsync = (bool)cfg.GetValue("video", "vsync", _vsync);
		_fpsIndex = Mathf.Clamp((int)cfg.GetValue("video", "fps_limit", _fpsIndex), 0, FpsLimits.Length - 1);
		_showFps = (bool)cfg.GetValue("video", "show_fps", _showFps);
	}
}
