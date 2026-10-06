extends SceneTree

func _initialize() -> void:
	var pack := OS.get_environment("LANTERNWAKE_AUDIT_PACK")
	if not pack.is_absolute_path() or not ProjectSettings.load_resource_pack(pack):
		push_error("Cannot mount owned diagnostic data archive")
		quit(1)
		return
	# Load the archive's binary target explicitly; do not fall back to source TSCN.
	var remap := ConfigFile.new()
	if remap.load("res://Scenes/UI/GameInterface.tscn.remap") != OK:
		push_error("Missing exported interface remap")
		quit(1)
		return
	var target: String = remap.get_value("remap", "path", "")
	var scene := load(target) as PackedScene
	if scene == null:
		push_error("Cannot load archived binary interface")
		quit(1)
		return
	var ui := scene.instantiate()
	root.add_child(ui)
	var panel := ui.get_node("TopBar/TopRow/Titles") as PanelContainer
	var padding := ui.get_node("TopBar/TopRow/Titles/Padding") as MarginContainer
	var chapter := ui.get_node("%ChapterLabel") as Label
	var place := ui.get_node("%PlaceLabel") as Label
	var style := panel.get_theme_stylebox("panel") as StyleBoxFlat
	if panel == null or padding == null or chapter == null or place == null or style == null:
		push_error("Archived header bindings/style failed")
		quit(1)
		return
	if padding.get_theme_constant("margin_left") != 12 or padding.get_theme_constant("margin_top") != 3 or style.bg_color.r >= 0.15 or style.bg_color.g >= 0.15 or style.bg_color.b >= 0.15:
		push_error("Archived header backing/padding differs")
		quit(1)
		return
	print("LANTERNWAKE_EXPORTED_HEADER_OK " + JSON.stringify({"binaryResource": target, "panel": panel.get_class(), "paddingLeft": 12, "paddingTop": 3, "background": str(style.bg_color), "chapterFont": chapter.get_theme_font_size("font_size"), "placeFont": place.get_theme_font_size("font_size")}))
	ui.free()
	quit(0)
