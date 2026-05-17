using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private const int HudMargin = 16;

    private void BuildUi()
    {
        // UI is put on a CanvasLayer so it stays fixed on screen while the
        // Camera2D pans and zooms the world map underneath it.
        var canvas = new CanvasLayer();
        AddChild(canvas);

        _menuRoot = BuildMainMenu();
        canvas.AddChild(_menuRoot);

        _newGameRoot = BuildNewGameSetup();
        canvas.AddChild(_newGameRoot);

        _gameRoot = BuildGameHud();
        canvas.AddChild(_gameRoot);

        _optionsPanel = BuildOptionsPanel();
        canvas.AddChild(_optionsPanel);
    }

    private Control BuildMainMenu()
    {
        // The main menu is intentionally small: it only decides whether the game
        // starts from a fresh generated world or from the single save slot.
        var root = BuildFullscreenRoot();
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(260, 190)
        };
        ApplyPanelChrome(panel);
        center.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "StrategyGame",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var newGame = new Button { Text = "New Game" };
        ApplyButtonChrome(newGame);
        newGame.Pressed += ShowNewGameSetup;
        box.AddChild(newGame);

        _loadGameButton = new Button { Text = "Load Game" };
        ApplyButtonChrome(_loadGameButton);
        _loadGameButton.Pressed += LoadGameFromMenu;
        box.AddChild(_loadGameButton);

        var options = new Button { Text = "Options" };
        ApplyButtonChrome(options);
        options.Pressed += ShowOptionsPanel;
        box.AddChild(options);

        var exit = new Button { Text = "Exit" };
        ApplyButtonChrome(exit);
        exit.Pressed += () => GetTree().Quit();
        box.AddChild(exit);

        return root;
    }

    private Control BuildNewGameSetup()
    {
        // New game setup exposes the worldgen knobs before committing to the
        // relatively expensive map build.
        var root = BuildFullscreenRoot();
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(520, 560)
        };
        ApplyPanelChrome(panel);
        center.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "New World",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        _mapSizeSlider = AddSlider(box, "Map size", WorldGenerationSettings.MinMapSize, WorldGenerationSettings.MaxMapSize, 16, WorldGenerationSettings.DefaultMapSize, out _mapSizeValueLabel, FormatMapSize);
        _civilizationsSlider = AddSlider(box, "Civilizations", WorldGenerationSettings.MinCivilizations, WorldGenerationSettings.MaxCivilizations, 1, WorldGenerationSettings.DefaultCivilizations, out _civilizationsValueLabel, v => $"{(int)v}");
        _wetnessSlider = AddSlider(box, "Wetness", 0, 100, 1, 50, out _wetnessValueLabel, v => $"{(int)v}%");
        _grasslandShrublandSlider = AddSlider(box, "Grassland 0 - 100 Shrubland", 0, 100, 1, 35, out _grasslandShrublandValueLabel, v => $"{(int)v}%");
        _desertBadlandsSlider = AddSlider(box, "Desert 0 - 100 Badlands", 0, 100, 1, 25, out _desertBadlandsValueLabel, v => $"{(int)v}%");
        _coniferBroadleafSlider = AddSlider(box, "Conifer 0 - 100 Broadleaf", 0, 100, 1, 50, out _coniferBroadleafValueLabel, v => $"{(int)v}%");
        _elevationSlider = AddSlider(box, "Elevation variance", 0, 100, 1, 50, out _elevationValueLabel, v => $"{(int)v}%");
        _maxSeaSlider = AddSlider(box, "Max sea number", 0, 5, 1, 2, out _maxSeaValueLabel, v => $"{(int)v}");
        _climateBiasSlider = AddSlider(box, "Climate bias", -1, 1, 1, 0, out _climateBiasValueLabel, FormatClimateBias);

        box.AddChild(new HSeparator());
        box.AddChild(new Label
        {
            Text = "Allowed races",
            HorizontalAlignment = HorizontalAlignment.Center
        });
        AddRaceAllowedControls(box);

        var buttons = new HBoxContainer();
        box.AddChild(buttons);

        var back = new Button { Text = "Back" };
        ApplyButtonChrome(back);
        back.Pressed += ShowMainMenu;
        buttons.AddChild(back);

        var create = new Button { Text = "Create" };
        ApplyButtonChrome(create);
        create.Pressed += CreateNewGameFromSetup;
        buttons.AddChild(create);

        return root;
    }

    private void AddRaceAllowedControls(VBoxContainer parent)
    {
        _raceAllowedChecks.Clear();
        var grid = new GridContainer { Columns = 3 };
        parent.AddChild(grid);

        foreach (var faction in _database.Factions.Values.OrderBy(faction => faction.Name))
        {
            var isUndead = faction.Id.Equals("undead", StringComparison.OrdinalIgnoreCase);
            var check = new CheckBox
            {
                Text = faction.Name,
                ButtonPressed = !isUndead
            };
            check.Toggled += _ => RefreshCivilizationLimit();
            grid.AddChild(check);
            _raceAllowedChecks[faction.Id] = check;
        }

        RefreshCivilizationLimit();
    }

    private static HSlider AddSlider(VBoxContainer parent, string caption, double min, double max, double step, double value, out Label valueLabel, Func<double, string> formatter)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);

        row.AddChild(new Label
        {
            Text = caption,
            CustomMinimumSize = new Vector2(210, 0)
        });

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(slider);

        valueLabel = new Label
        {
            Text = formatter(value),
            CustomMinimumSize = new Vector2(88, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        row.AddChild(valueLabel);

        var capturedLabel = valueLabel;
        slider.ValueChanged += next => capturedLabel.Text = formatter(next);
        return slider;
    }

    private static string FormatMapSize(double value)
    {
        var size = (int)value;
        return $"{size}x{size}";
    }

    private static string FormatClimateBias(double value)
    {
        return (int)value switch
        {
            < 0 => "Cold",
            > 0 => "Hot",
            _ => "Normal"
        };
    }

    private Control BuildGameHud()
    {
        var root = BuildFullscreenRoot();

        _gearButton = new Button
        {
            Text = "\u2699",
            TooltipText = "Menu",
            CustomMinimumSize = new Vector2(44, 44)
        };
        ApplyButtonChrome(_gearButton);
        _gearButton.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _gearButton.Position = new Vector2(-HudMargin - _gearButton.CustomMinimumSize.X, HudMargin);
        _gearButton.Pressed += ToggleGearMenu;
        root.AddChild(_gearButton);

        _gearMenuPanel = BuildGearMenu();
        root.AddChild(_gearMenuPanel);

        var leftPanel = BuildHudPanel(new Vector2(HudMargin, -240), Control.LayoutPreset.BottomLeft, new Vector2(480, 220), out var leftBox);

        var actionRow = new HBoxContainer();
        leftBox.AddChild(actionRow);

        _stationGroupButton = new Button { Text = "Garrison", Visible = false };
        ApplyButtonChrome(_stationGroupButton);
        _stationGroupButton.Pressed += StationSelectedGroup;
        actionRow.AddChild(_stationGroupButton);

        _deployGroupButton = new Button { Text = "Mobilize", Visible = false };
        ApplyButtonChrome(_deployGroupButton);
        _deployGroupButton.Pressed += DeployStationedGroup;
        actionRow.AddChild(_deployGroupButton);

        _relocateCiviliansButton = new Button { Text = "Relocate", Visible = false };
        ApplyButtonChrome(_relocateCiviliansButton);
        _relocateCiviliansButton.Pressed += RelocateCiviliansFromInspectedLocation;
        actionRow.AddChild(_relocateCiviliansButton);

        _settleCiviliansButton = new Button { Text = "Settle", Visible = false };
        ApplyButtonChrome(_settleCiviliansButton);
        _settleCiviliansButton.Pressed += SettleSelectedCivilians;
        actionRow.AddChild(_settleCiviliansButton);

        _transferUnitsButton = new Button { Text = "Transfer Units", Visible = false };
        ApplyButtonChrome(_transferUnitsButton);
        _transferUnitsButton.Pressed += TransferUnitsToSelectedGroup;
        actionRow.AddChild(_transferUnitsButton);

        _splitGroupButton = new Button { Text = "Split Group", Visible = false };
        ApplyButtonChrome(_splitGroupButton);
        _splitGroupButton.Pressed += SplitSelectedGroup;
        actionRow.AddChild(_splitGroupButton);

        _renameGroupButton = new Button { Text = "Rename", Visible = false };
        ApplyButtonChrome(_renameGroupButton);
        _renameGroupButton.Pressed += RenameSelectedGroup;
        actionRow.AddChild(_renameGroupButton);

        _selectionInfoLabel = BuildInfoText();
        leftBox.AddChild(_selectionInfoLabel);
        root.AddChild(leftPanel);

        _actionMenuPanel = BuildActionMenu();
        root.AddChild(_actionMenuPanel);

        _endTurnButton = new Button
        {
            Text = "End Turn",
            CustomMinimumSize = new Vector2(190, 36)
        };
        ApplyButtonChrome(_endTurnButton);
        _endTurnButton.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _endTurnButton.Position = new Vector2(-HudMargin - _endTurnButton.CustomMinimumSize.X, -280);
        _endTurnButton.Pressed += EndPlayerTurn;
        root.AddChild(_endTurnButton);

        var rightPanel = BuildHudPanel(new Vector2(-HudMargin - 480, -240), Control.LayoutPreset.BottomRight, new Vector2(480, 220), out var rightBox);
        _tileInfoLabel = BuildInfoText();
        rightBox.AddChild(_tileInfoLabel);
        root.AddChild(rightPanel);

        _logPanel = BuildLogPanel();
        root.AddChild(_logPanel);

        _logToggleButton = new Button
        {
            Text = "Log ^",
            Visible = false,
            CustomMinimumSize = new Vector2(100, 32)
        };
        ApplyButtonChrome(_logToggleButton);
        _logToggleButton.AnchorLeft = 0.5f;
        _logToggleButton.AnchorRight = 0.5f;
        _logToggleButton.AnchorTop = 1f;
        _logToggleButton.AnchorBottom = 1f;
        _logToggleButton.OffsetLeft = -50;
        _logToggleButton.OffsetTop = -40;
        _logToggleButton.OffsetRight = 50;
        _logToggleButton.OffsetBottom = -8;
        _logToggleButton.Pressed += ExpandLog;
        root.AddChild(_logToggleButton);

        _exitConfirmOverlay = BuildExitConfirmOverlay();
        root.AddChild(_exitConfirmOverlay);

        return root;
    }

    private Control BuildGearMenu()
    {
        var panel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(180, 140)
        };
        ApplyPanelChrome(panel);
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.Position = new Vector2(-HudMargin - 180, HudMargin + 48);

        var box = new VBoxContainer();
        panel.AddChild(box);

        var saveGame = new Button { Text = "Save" };
        ApplyButtonChrome(saveGame);
        saveGame.Pressed += SaveGameFromMenu;
        box.AddChild(saveGame);

        var saveAndExit = new Button { Text = "Save and Exit" };
        ApplyButtonChrome(saveAndExit);
        saveAndExit.Pressed += SaveAndExitFromMenu;
        box.AddChild(saveAndExit);

        var options = new Button { Text = "Options" };
        ApplyButtonChrome(options);
        options.Pressed += ShowOptionsPanel;
        box.AddChild(options);

        var exit = new Button { Text = "Exit" };
        ApplyButtonChrome(exit);
        exit.Pressed += PromptExitWithoutSave;
        box.AddChild(exit);

        return panel;
    }

    private Control BuildOptionsPanel()
    {
        var root = BuildFullscreenRoot();
        root.Visible = false;
        root.MouseFilter = Control.MouseFilterEnum.Stop;

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.45f)
        };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(430, 360)
        };
        ApplyPanelChrome(panel);
        center.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "Options",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        AddSlider(box, "Effects", 0, 100, 1, _settings.EffectsVolume, out _, v => $"{(int)v}%")
            .ValueChanged += value =>
            {
                _settings.EffectsVolume = (int)value;
                SavePresentationSettings();
            };

        AddSlider(box, "Music", 0, 100, 1, _settings.MusicVolume, out _, v => $"{(int)v}%")
            .ValueChanged += value =>
            {
                _settings.MusicVolume = (int)value;
                SavePresentationSettings();
            };

        var displayRow = new HBoxContainer();
        box.AddChild(displayRow);

        _gridVisibleCheckBox = new CheckBox
        {
            Text = "Grid (G)",
            ButtonPressed = _settings.GridVisible
        };
        _gridVisibleCheckBox.Toggled += enabled => SetGridVisibilityPreference(enabled);
        displayRow.AddChild(_gridVisibleCheckBox);

        _resourceIconsVisibleCheckBox = new CheckBox
        {
            Text = "Resources (R)",
            ButtonPressed = _settings.ResourceIconsVisible
        };
        _resourceIconsVisibleCheckBox.Toggled += enabled => SetResourceIconsVisibilityPreference(enabled);
        displayRow.AddChild(_resourceIconsVisibleCheckBox);

        var speedRow = new HBoxContainer();
        box.AddChild(speedRow);
        speedRow.AddChild(new Label
        {
            Text = "Animation speed",
            CustomMinimumSize = new Vector2(160, 0)
        });

        var speed = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var speedOptions = new[]
        {
            AnimationSpeedSetting.Slow,
            AnimationSpeedSetting.Medium,
            AnimationSpeedSetting.Fast,
            AnimationSpeedSetting.Immediate
        };
        foreach (var option in speedOptions)
        {
            speed.AddItem(option.ToString(), (int)option);
        }
        speed.Select(Array.IndexOf(speedOptions, _settings.AnimationSpeed));
        speed.ItemSelected += index =>
        {
            _settings.AnimationSpeed = (AnimationSpeedSetting)speed.GetItemId((int)index);
            SavePresentationSettings();
        };
        speedRow.AddChild(speed);

        box.AddChild(new HSeparator());
        box.AddChild(new Label
        {
            Text = "Controls",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(new Label { Text = "", CustomMinimumSize = new Vector2(150, 0) });
        header.AddChild(new Label { Text = "1st", CustomMinimumSize = new Vector2(90, 0), HorizontalAlignment = HorizontalAlignment.Center });
        header.AddChild(new Label { Text = "2nd", CustomMinimumSize = new Vector2(90, 0), HorizontalAlignment = HorizontalAlignment.Center });

        var openMenuRow = BuildControlBindingRow("Open Menu", KeyLabel(_settings.KeyBindings.OpenMenuPrimary), false);
        _openMenuSecondaryButton = BuildBindingButton(_settings.KeyBindings.OpenMenuSecondary, () => BeginKeyBindingCapture("OpenMenuSecondary"));
        openMenuRow.AddChild(_openMenuSecondaryButton);
        box.AddChild(openMenuRow);

        var recenterRow = BuildControlBindingRow("Recenter", null, true);
        _recenterPrimaryButton = BuildBindingButton(_settings.KeyBindings.RecenterPrimary, () => BeginKeyBindingCapture("RecenterPrimary"));
        _recenterSecondaryButton = BuildBindingButton(_settings.KeyBindings.RecenterSecondary, () => BeginKeyBindingCapture("RecenterSecondary"));
        recenterRow.AddChild(_recenterPrimaryButton);
        recenterRow.AddChild(_recenterSecondaryButton);
        box.AddChild(recenterRow);

        var close = new Button { Text = "Close" };
        ApplyButtonChrome(close);
        close.Pressed += HideOptionsPanel;
        box.AddChild(close);

        return root;
    }

    private HBoxContainer BuildControlBindingRow(string label, string? lockedPrimaryText, bool primaryEditable)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(150, 0)
        });

        if (!primaryEditable)
        {
            var primary = new Button
            {
                Text = lockedPrimaryText ?? "-",
                Disabled = true,
                CustomMinimumSize = new Vector2(90, 28)
            };
            ApplyButtonChrome(primary);
            row.AddChild(primary);
        }

        return row;
    }

    private Button BuildBindingButton(int key, Action onPressed)
    {
        var button = new Button
        {
            Text = KeyLabel(key),
            CustomMinimumSize = new Vector2(90, 28)
        };
        ApplyButtonChrome(button);
        button.Pressed += onPressed;
        return button;
    }

    private void BeginKeyBindingCapture(string binding)
    {
        _capturingKeyBinding = binding;
        RefreshControlBindingButtons();
    }

    private void RefreshControlBindingButtons()
    {
        if (_openMenuSecondaryButton is not null)
        {
            _openMenuSecondaryButton.Text = _capturingKeyBinding == "OpenMenuSecondary" ? "Press key..." : KeyLabel(_settings.KeyBindings.OpenMenuSecondary);
        }

        if (_recenterPrimaryButton is not null)
        {
            _recenterPrimaryButton.Text = _capturingKeyBinding == "RecenterPrimary" ? "Press key..." : KeyLabel(_settings.KeyBindings.RecenterPrimary);
        }

        if (_recenterSecondaryButton is not null)
        {
            _recenterSecondaryButton.Text = _capturingKeyBinding == "RecenterSecondary" ? "Press key..." : KeyLabel(_settings.KeyBindings.RecenterSecondary);
        }
    }

    private static string KeyLabel(int key)
    {
        return key == 0 ? "-" : ((Key)key).ToString();
    }

    private Control BuildActionMenu()
    {
        var panel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(440, 180)
        };
        ApplyPanelChrome(panel);
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        panel.Position = new Vector2(HudMargin + 170, -310);

        _actionMenuButtons = new VBoxContainer();
        panel.AddChild(_actionMenuButtons);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(620, 140)
        };
        ApplyPanelChrome(panel);
        panel.AnchorLeft = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorTop = 1f;
        panel.AnchorBottom = 1f;
        panel.OffsetLeft = -310;
        panel.OffsetTop = -184;
        panel.OffsetRight = 310;
        panel.OffsetBottom = -44;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        margin.AddChild(box);

        var header = new HBoxContainer();
        box.AddChild(header);

        _logHeaderLabel = new Label
        {
            Text = "Turn 1: Faction",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        header.AddChild(_logHeaderLabel);

        var minimize = new Button
        {
            Text = "-",
            CustomMinimumSize = new Vector2(28, 28)
        };
        ApplyButtonChrome(minimize);
        minimize.Pressed += CollapseLog;
        header.AddChild(minimize);

        _logLabel = BuildInfoText();
        _logLabel.FitContent = false;
        _logLabel.ScrollActive = true;
        _logLabel.CustomMinimumSize = new Vector2(0, 92);
        box.AddChild(_logLabel);
        return panel;
    }

    private Control BuildExitConfirmOverlay()
    {
        var root = BuildFullscreenRoot();
        root.Visible = false;
        root.MouseFilter = Control.MouseFilterEnum.Stop;

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.45f)
        };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(320, 120)
        };
        ApplyPanelChrome(panel);
        center.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "Exit without saving?",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        box.AddChild(buttons);

        var yes = new Button { Text = "Yes" };
        ApplyButtonChrome(yes);
        yes.Pressed += ConfirmExitWithoutSave;
        buttons.AddChild(yes);

        var no = new Button { Text = "No" };
        ApplyButtonChrome(no);
        no.Pressed += CancelExitWithoutSave;
        buttons.AddChild(no);

        return root;
    }

    private static Control BuildFullscreenRoot()
    {
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return root;
    }

    private static PanelContainer BuildHudPanel(Vector2 position, Control.LayoutPreset preset, Vector2 minimumSize, out VBoxContainer box)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimumSize
        };
        ApplyPanelChrome(panel);
        panel.SetAnchorsPreset(preset);
        panel.Position = position;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(margin);
        box = new VBoxContainer();
        margin.AddChild(box);
        return panel;
    }

    private static RichTextLabel BuildInfoText()
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = false
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#202327"),
            BorderColor = new Color("#70757d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6
        };
        label.AddThemeStyleboxOverride("normal", style);
        return label;
    }

    private static void ApplyPanelChrome(Control control)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#202327"),
            BorderColor = new Color("#70757d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8
        };
        control.AddThemeStyleboxOverride("panel", style);
    }

    private static void ApplyButtonChrome(Button button)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color("#2b2f35"),
            BorderColor = new Color("#70757d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4
        };
        var hover = normal.Duplicate() as StyleBoxFlat ?? new StyleBoxFlat();
        hover.BgColor = new Color("#343a42");
        var pressed = normal.Duplicate() as StyleBoxFlat ?? new StyleBoxFlat();
        pressed.BgColor = new Color("#1f2328");
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", normal);
    }

}
