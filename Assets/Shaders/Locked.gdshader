shader_type canvas_item;

uniform vec4 silhouette_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    // Keep original alpha, replace RGB with silhouette color
    COLOR = vec4(silhouette_color.rgb, tex.a);
}
