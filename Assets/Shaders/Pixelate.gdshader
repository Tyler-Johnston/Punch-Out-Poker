shader_type canvas_item;

// How many color "steps" you want (lower = more retro/banded)
uniform float levels : hint_range(1.0, 32.0) = 40.0;

void fragment() {
    // Get the smooth color from the texture
    vec4 color = texture(TEXTURE, UV);
    
    // "Snap" the color channels to the nearest level
    color.rgb = floor(color.rgb * levels) / levels;
    
    COLOR = color;
}
