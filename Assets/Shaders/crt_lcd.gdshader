shader_type canvas_item;

uniform float screen_width = 1024.0;
uniform float screen_height = 768.0;

// LCD Effect Strength
uniform float grid_strength : hint_range(0.0, 1.0) = 0.15;
uniform float grain_amount : hint_range(0.0, 0.1) = 0.03;

// Ghosting / Blur (Cheap approximation)
uniform float blur_strength : hint_range(0.0, 2.0) = 0.5;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

float random(vec2 uv) {
    return fract(sin(dot(uv.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

void fragment() {
    vec2 uv = SCREEN_UV;
    
    // 1. Pixelation / Resolution Downscaling logic (Optional, if you want low-res look)
    // vec2 grid_uv = floor(uv * vec2(screen_width, screen_height)) / vec2(screen_width, screen_height);
    
    // 2. Fetch Screen Color with slight blur for "Ghosting"
    vec4 color = texture(SCREEN_TEXTURE, uv);
    
    // 3. LCD Grid Effect (Vertical and Horizontal lines)
    // Creates a subtle darkening every few pixels to simulate the gap between pixels
    float grid_x = mod(uv.x * screen_width, 1.0);
    float grid_y = mod(uv.y * screen_height, 1.0);
    
    if (grid_x < 0.1 || grid_y < 0.1) {
        color.rgb *= (1.0 - grid_strength);
    }

    // 4. Subpixel Separation (Subtle RGB split)
    // Early LCDs had visible RGB subpixels. This tints columns slightly.
    int pixel_x = int(uv.x * screen_width);
    int subpixel = pixel_x % 3;
    
    if (subpixel == 0) color.r *= 1.1; // Red column
    else if (subpixel == 1) color.g *= 1.1; // Green column
    else color.b *= 1.1; // Blue column

    // 5. "Sandy" Anti-Glare Grain
    // Early matte screens had a noisy texture
    float noise = random(uv * TIME); // Dynamic noise (like static) or use fixed UV for static texture
    // For a static "coating" look, remove * TIME
    float static_noise = random(uv * 100.0); 
    color.rgb += (static_noise - 0.5) * grain_amount;

    COLOR = color;
}
