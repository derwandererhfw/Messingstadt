using Godot;

namespace Messingstadt
{
    /// <summary>Bindet das Bild wie einen alten Film zusammen: dunkle Ränder, feines Korn, leichte Farbstimmung.</summary>
    public partial class FilmLook : CanvasLayer
    {
        private const string Code = @"
shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_nearest;
float grain(vec2 p) {
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}
void fragment() {
    vec3 c = texture(screen, SCREEN_UV).rgb;
    float lum = dot(c, vec3(0.3, 0.59, 0.11));
    // Etwas weniger Farbe, Schatten leicht kühl, Lichter leicht warm
    c = mix(c, vec3(lum), 0.12);
    c = mix(c * vec3(0.95, 0.99, 1.07), c * vec3(1.05, 1.0, 0.92), smoothstep(0.1, 0.7, lum));
    // Dunkle Ränder wie bei einer alten Optik
    float d = distance(UV, vec2(0.5)) * 1.28;
    c *= 1.0 - smoothstep(0.5, 1.05, d) * 0.62;
    // Korn wechselt zwölfmal in der Sekunde, im Dunkeln sichtbarer
    float t = floor(TIME * 12.0);
    float n = grain(floor(SCREEN_UV * vec2(640.0, 400.0)) + t) - 0.5;
    c += n * mix(0.05, 0.02, lum);
    COLOR = vec4(c, 1.0);
}";

        public override void _Ready()
        {
            AddChild(new ColorRect
            {
                Size = new Vector2(1280, 800), MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new ShaderMaterial { Shader = new Shader { Code = Code } },
            });
        }
    }
}
