shader_type canvas_item;

uniform float noise_amount : hint_range(0.0, 1.0) = 0.05;
uniform float static_intensity : hint_range(0.0, 1.0) = 0.02;
uniform float scanline_alpha : hint_range(0.0, 1.0) = 0.1;
uniform vec4 tint_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float distortion : hint_range(0.0, 0.1) = 0.02; // Lens curve

float random(vec2 uv) {
    return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453123);
}

void fragment() {
    vec2 uv = UV;
    
    // 1. Lens Distortion (Subtle Curve)
    vec2 centered_uv = uv - 0.5;
    float r = dot(centered_uv, centered_uv);
    uv = uv + centered_uv * (r * distortion);
    
    // Check bounds after distortion (black bars if outside)
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
        COLOR = vec4(0.0, 0.0, 0.0, 1.0);
    } else {
        vec4 tex_color = texture(TEXTURE, uv);
        
        // 2. Scanlines
        float scanline = sin(uv.y * 800.0 * PI) * 0.5 + 0.5;
        tex_color.rgb -= scanline * scanline_alpha;
        
        // 3. Static / Noise
        float noise = random(uv + vec2(TIME * 10.0, TIME * 50.0));
        tex_color.rgb += (noise - 0.5) * noise_amount;
        
        // 4. Glitch / Static Blocks (controlled by static_intensity)
        if (random(vec2(TIME, uv.y)) < static_intensity) {
             vec2 offset = vec2(random(vec2(TIME, uv.y)) * 0.05, 0.0);
             tex_color = texture(TEXTURE, uv + offset);
             tex_color.g += 0.2; // Green chromatic aberration
        }

        COLOR = tex_color * tint_color;
    }
}
