shader_type canvas_item;

// 64.0 means the image will look like it is 64x64 pixels wide/tall.
uniform float pixel_amount : hint_range(1.0, 512.0) = 64.0; 

void fragment() {
    // 1. Calculate the "grid" size
    float grid_uv_x = round(UV.x * pixel_amount) / pixel_amount;
    float grid_uv_y = round(UV.y * pixel_amount) / pixel_amount;
    
    // 2. Sample the texture at the "snapped" coordinate instead of the smooth one
    vec4 text = texture(TEXTURE, vec2(grid_uv_x, grid_uv_y));
    
    COLOR = text;
}
