shader_type canvas_item;

// --- SETTINGS ---
uniform sampler2D gradient_texture : source_color, filter_linear;
uniform vec4 border_color : source_color = vec4(0.4, 0.2, 0.1, 1.0); // Brown
uniform float border_width : hint_range(0.0, 0.2) = 0.02;

// Perspective & Shape
uniform float tilt : hint_range(1.0, 10.0) = 3.0; // Squish factor
uniform float scale : hint_range(0.1, 5.0) = 0.8; // Size of table

void fragment() {
    // 1. Center UVs
    vec2 uv = UV - 0.5;
    
    // 2. Apply Perspective Squish
    uv.y *= tilt; 

    // 3. Calculate Distance
    // We multiply by 2.0 so "1.0" is exactly the edge of our unit circle
    float dist = length(uv) * (1.0 / scale) * 2.0;
    
    // 4. Sample Gradient (Clamp to avoid artifacts at edge)
    vec4 table_color = texture(gradient_texture, vec2(clamp(dist, 0.0, 1.0), 0.0));
    
    // 5. Logic for Border vs. Table vs. Transparent Background
    // If dist < 1.0, we are inside the table.
    // If dist > 1.0 but < 1.0 + border_width, we are on the border.
    // If dist > 1.0 + border_width, we are outside (transparent).
    
    float is_border = step(1.0, dist);        // 0 if table, 1 if border/outside
    float is_outside = step(1.0 + border_width, dist); // 0 if table/border, 1 if outside
    
    // Mix the Table Color with Border Color
    vec4 final_color = mix(table_color, border_color, is_border);
    
    // Apply Transparency (Cut out the background)
    // If we are "outside", alpha becomes 0.0. Otherwise it stays 1.0.
    final_color.a = 1.0 - is_outside;
    
    COLOR = final_color;
}
