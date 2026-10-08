shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture :
    hint_screen_texture, repeat_disable, filter_linear_mipmap;

uniform float blur_amount : hint_range(0.0, 5.0, 0.1) = 1.5;

void fragment() {
    vec4 panel_color = COLOR;
    vec3 blurred = textureLod(
        screen_texture, SCREEN_UV, blur_amount
    ).rgb;

    COLOR = vec4(
        mix(blurred, panel_color.rgb, panel_color.a),
        1.0
    );
}