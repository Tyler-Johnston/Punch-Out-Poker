shader_type canvas_item;

// --- TABLE SHAPE SETTINGS ---
uniform sampler2D gradient_texture : source_color, filter_linear;
uniform vec4 border_color : source_color = vec4(0.4, 0.2, 0.1, 1.0);
uniform float border_width : hint_range(0.0, 0.2) = 0.02;
uniform float tilt : hint_range(1.0, 10.0) = 3.0;
uniform float scale : hint_range(0.1, 5.0) = 0.8;

// --- PIXELATION SETTINGS ---
// Controls how "blocky" the shape is.
// 0.001 = Ultra High Res (Smooth)
// 0.01 = Retro
// 0.05 = Very Chunky
uniform float pixel_factor : hint_range(0.001, 0.1) = 0.01;

// --- COLOR BANDING SETTINGS ---
uniform float levels : hint_range(1.0, 32.0) = 25.0;

void fragment() {
    // --- STEP 1: PIXELATE THE COORDINATES ---
    // Instead of using smooth "UV", we snap UV to a grid.
    // simpler method: floor(uv / size) * size
    vec2 pixel_uv = floor(UV / pixel_factor) * pixel_factor;
    
    // --- STEP 2: CALCULATE SHAPE (Using pixel_uv) ---
    vec2 uv = pixel_uv - 0.5;
    uv.y *= tilt; // Squish vertical axis

    // Calculate Distance
    float dist = length(uv) * (1.0 / scale) * 2.0;
    
    // Sample Gradient
    vec4 table_color = texture(gradient_texture, vec2(clamp(dist, 0.0, 1.0), 0.0));
    
    // Define Regions
    float is_border = step(1.0, dist);        
    float is_outside = step(1.0 + border_width, dist); 
    
    // Mix Table and Border
    vec4 final_color = mix(table_color, border_color, is_border);
    
    // --- STEP 3: APPLY COLOR BANDING ---
    final_color.rgb = floor(final_color.rgb * levels) / levels;
    
    // --- STEP 4: APPLY TRANSPARENCY ---
    final_color.a = 1.0 - is_outside;
    
    COLOR = final_color;
}
