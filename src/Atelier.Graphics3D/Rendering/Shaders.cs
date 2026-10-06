namespace Atelier.Graphics3D.Rendering;

// GLSL 3.30 core sources. Shading happens in linear light into a floating-point target with premultiplied alpha
// (cleared to transparent); the composite pass tone-maps it over the background gradient.
internal static class Shaders
{
    public const string MeshVertex = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aTexCoord;
        layout(location = 3) in vec4 aColor;

        uniform mat4 uModel;
        uniform mat4 uNormalMatrix;
        uniform mat4 uViewProjection;
        uniform float uPointSize;

        out vec3 vWorldPos;
        out vec3 vNormal;
        out vec2 vTexCoord;
        out vec4 vColor;

        void main()
        {
            vec4 world = uModel * vec4(aPosition, 1.0);
            vWorldPos = world.xyz;
            vNormal = mat3(uNormalMatrix) * aNormal;
            vTexCoord = aTexCoord;
            vColor = aColor;
            gl_Position = uViewProjection * world;
            gl_PointSize = uPointSize;
        }
        """;

    public const string MeshFragment = """
        #version 330 core
        in vec3 vWorldPos;
        in vec3 vNormal;
        in vec2 vTexCoord;
        in vec4 vColor;

        uniform vec4 uBaseColor;          // linear, straight alpha
        uniform sampler2D uBaseColorTexture;
        uniform sampler2D uNormalTexture;
        uniform int uHasBaseColorTexture;
        uniform int uHasNormalTexture;
        uniform int uHasNormals;
        uniform float uNormalScale;
        uniform float uRoughness;
        uniform float uMetallic;
        uniform float uTransmission;
        uniform int uUnlit;
        uniform int uRoundPoints;
        uniform int uOverride;            // wireframe overlay: draw uOverrideColor
        uniform vec4 uOverrideColor;

        uniform vec3 uCameraPos;
        uniform vec3 uViewDir;            // towards the camera, for orthographic views
        uniform int uOrthographic;
        uniform vec3 uSunDir;
        uniform vec3 uSunRadiance;
        uniform vec3 uSkyColor;
        uniform vec3 uGroundColor;

        out vec4 fragColor;

        const float PI = 3.14159265;

        // Normal mapping without tangents: the cotangent frame from screen-space derivatives (Schüler, 2013).
        vec3 perturbNormal(vec3 N, vec3 p, vec2 uv)
        {
            vec3 dp1 = dFdx(p);
            vec3 dp2 = dFdy(p);
            vec2 duv1 = dFdx(uv);
            vec2 duv2 = dFdy(uv);
            vec3 dp2perp = cross(dp2, N);
            vec3 dp1perp = cross(N, dp1);
            vec3 T = dp2perp * duv1.x + dp1perp * duv2.x;
            vec3 B = dp2perp * duv1.y + dp1perp * duv2.y;
            float len = max(dot(T, T), dot(B, B));
            if (len < 1e-20) return N;
            float invmax = inversesqrt(len);
            vec3 m = texture(uNormalTexture, uv).xyz * 2.0 - 1.0;
            m.xy *= uNormalScale;
            // B points towards increasing v, which runs down the image; the OpenGL convention's green points up.
            m.y = -m.y;
            return normalize(mat3(T * invmax, B * invmax, N) * m);
        }

        float distributionGGX(float NdotH, float a)
        {
            float a2 = a * a;
            float d = NdotH * NdotH * (a2 - 1.0) + 1.0;
            return a2 / (PI * d * d);
        }

        float visibilitySmith(float NdotV, float NdotL, float a)
        {
            float k = a * 0.5;
            float gv = NdotV / (NdotV * (1.0 - k) + k);
            float gl = NdotL / (NdotL * (1.0 - k) + k);
            return gv * gl / max(4.0 * NdotV * NdotL, 1e-4);
        }

        void main()
        {
            if (uRoundPoints != 0)
            {
                vec2 c = gl_PointCoord * 2.0 - 1.0;
                if (dot(c, c) > 1.0) discard;
            }
            if (uOverride != 0)
            {
                fragColor = vec4(uOverrideColor.rgb * uOverrideColor.a, uOverrideColor.a);
                return;
            }

            vec4 base = uBaseColor * vColor;
            if (uHasBaseColorTexture != 0) base *= texture(uBaseColorTexture, vTexCoord);
            if (base.a <= 0.002) discard;

            vec3 color;
            if (uUnlit != 0 || uHasNormals == 0)
            {
                color = base.rgb;
            }
            else
            {
                vec3 N = normalize(vNormal);
                if (!gl_FrontFacing) N = -N;
                if (uHasNormalTexture != 0) N = perturbNormal(N, vWorldPos, vTexCoord);
                vec3 V = uOrthographic != 0 ? uViewDir : normalize(uCameraPos - vWorldPos);
                vec3 L = uSunDir;
                float NdotL = dot(N, L);
                float NdotV = max(dot(N, V), 1e-3);
                float a = max(uRoughness * uRoughness, 0.002);
                vec3 F0 = mix(vec3(0.04), base.rgb, uMetallic);
                vec3 diffuseColor = base.rgb * (1.0 - uMetallic);

                // Sun: Lambert diffuse plus GGX specular.
                vec3 direct = vec3(0.0);
                if (NdotL > 0.0)
                {
                    vec3 H = normalize(L + V);
                    float NdotH = max(dot(N, H), 0.0);
                    float VdotH = max(dot(V, H), 0.0);
                    vec3 F = F0 + (1.0 - F0) * pow(1.0 - VdotH, 5.0);
                    vec3 specular = F * distributionGGX(NdotH, a) * visibilitySmith(NdotV, NdotL, a);
                    direct = ((1.0 - F) * diffuseColor / PI + specular) * uSunRadiance * NdotL;
                }
                else if (uTransmission > 0.0)
                {
                    // Thin translucent fabric: sunlight through the cloth, tinted by it (the glow of a backlit canopy).
                    direct = diffuseColor * uTransmission * uSunRadiance * (-NdotL) / PI;
                }

                // Hemisphere ambient: sky from above, ground from below, plus a little environment specular.
                vec3 ambient = mix(uGroundColor, uSkyColor, N.y * 0.5 + 0.5);
                vec3 Fa = F0 + (max(vec3(1.0 - uRoughness), F0) - F0) * pow(1.0 - NdotV, 5.0);
                vec3 R = reflect(-V, N);
                vec3 env = mix(uGroundColor, uSkyColor, R.y * 0.5 + 0.5);
                color = direct + diffuseColor * ambient * (1.0 - Fa) + env * Fa * (1.0 - uRoughness * 0.7);

                // Soft rim from the sky, so silhouettes read against the background.
                float rim = pow(1.0 - NdotV, 3.0);
                color += uSkyColor * rim * 0.08;
            }

            fragColor = vec4(color * base.a, base.a);
        }
        """;

    // Full-screen triangle used by the grid and the composite pass.
    public const string FullscreenVertex = """
        #version 330 core
        out vec2 vUv;
        void main()
        {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            vUv = p;
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    // An infinite ground grid at y = uGridHeight: each pixel's view ray is intersected with the plane, the lines are
    // anti-aliased with screen-space derivatives and fade with the distance; depth is written so meshes hide it.
    // Everything is relative to the camera (the matrices have no translation, uGridOffset is the camera's position within
    // a major grid cell), so the numbers stay small and the lines stay crisp however far the camera is from the origin.
    public const string GridFragment = """
        #version 330 core
        in vec2 vUv;
        uniform mat4 uInverseViewProjection;  // camera-relative (view without translation)
        uniform mat4 uViewProjection;         // camera-relative
        uniform vec3 uCameraPos;              // world position, for the plane height and the axes
        uniform vec2 uGridOffset;             // camera x, z minus the nearest major grid corner
        uniform float uCellSize;      // minor line spacing
        uniform float uFadeDistance;
        uniform vec3 uLineColor;      // linear
        uniform float uLineAlpha;
        uniform float uGridHeight;    // the plane's y
        out vec4 fragColor;

        vec3 unproject(vec2 ndc, float z)
        {
            vec4 p = uInverseViewProjection * vec4(ndc, z, 1.0);
            return p.xyz / p.w;
        }

        float gridLines(vec2 coord, float lineWidth)
        {
            vec2 d = fwidth(coord);
            vec2 g = abs(fract(coord - 0.5) - 0.5) / max(d, vec2(1e-6));
            return 1.0 - min(min(g.x, g.y) / lineWidth, 1.0);
        }

        void main()
        {
            vec2 ndc = vUv * 2.0 - 1.0;
            vec3 nearP = unproject(ndc, -1.0);
            vec3 farP = unproject(ndc, 1.0);
            vec3 dir = farP - nearP;
            if (abs(dir.y) < 1e-6) discard;
            float t = (uGridHeight - uCameraPos.y - nearP.y) / dir.y;
            if (t < 0.0) discard;
            vec3 p = nearP + dir * t;   // relative to the camera

            vec4 clip = uViewProjection * vec4(p, 1.0);
            float depth = clip.z / clip.w;
            if (depth > 1.0) discard;
            gl_FragDepth = depth * 0.5 + 0.5;

            vec2 local = p.xz + uGridOffset;   // grid coordinates, small near the camera
            float minor = gridLines(local / uCellSize, 1.0);
            float major = gridLines(local / (uCellSize * 10.0), 1.5);
            float distanceFade = 1.0 - smoothstep(uFadeDistance * 0.35, uFadeDistance, length(p));
            // Grazing angles alias: fade the minor lines out as the view flattens.
            float grazing = clamp(abs(normalize(dir).y) * 4.0, 0.0, 1.0);

            vec2 world = p.xz + uCameraPos.xz;
            vec2 axisWidth = fwidth(p.xz) * 1.5;
            float xAxis = 1.0 - min(abs(world.y) / max(axisWidth.y, 1e-6), 1.0);   // the X axis runs along z = 0
            float zAxis = 1.0 - min(abs(world.x) / max(axisWidth.x, 1e-6), 1.0);   // the Z axis runs along x = 0

            float alpha = max(minor * 0.3 * grazing, major * 0.75) * uLineAlpha;
            vec3 color = uLineColor;
            if (xAxis > 0.0) { color = mix(color, vec3(0.80, 0.12, 0.14), xAxis); alpha = max(alpha, xAxis * 0.9); }
            if (zAxis > 0.0) { color = mix(color, vec3(0.12, 0.30, 0.85), zAxis); alpha = max(alpha, zAxis * 0.9); }
            alpha *= distanceFade;
            if (alpha <= 0.003) discard;
            fragColor = vec4(color * alpha, alpha);
        }
        """;

    // Tone maps the resolved scene (premultiplied, linear) and puts it over the background gradient (sRGB).
    public const string CompositeFragment = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uScene;
        uniform vec3 uBackgroundTop;     // sRGB
        uniform vec3 uBackgroundBottom;  // sRGB
        uniform float uExposure;
        out vec4 fragColor;

        // Khronos PBR Neutral tone mapper: keeps base colors faithful, compresses only highlights.
        vec3 neutralToneMap(vec3 color)
        {
            const float startCompression = 0.8 - 0.04;
            const float desaturation = 0.15;
            float x = min(color.r, min(color.g, color.b));
            float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
            color -= offset;
            float peak = max(color.r, max(color.g, color.b));
            if (peak < startCompression) return color;
            const float d = 1.0 - startCompression;
            float newPeak = 1.0 - d * d / (peak + d - startCompression);
            color *= newPeak / peak;
            float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
            return mix(color, newPeak * vec3(1.0), g);
        }

        vec3 linearToSrgb(vec3 c)
        {
            c = clamp(c, 0.0, 1.0);
            return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
        }

        void main()
        {
            vec4 scene = texelFetch(uScene, ivec2(gl_FragCoord.xy), 0);
            vec3 background = mix(uBackgroundBottom, uBackgroundTop, vUv.y);
            float a = clamp(scene.a, 0.0, 1.0);
            vec3 color = a > 1e-4 ? scene.rgb / a : vec3(0.0);
            vec3 mapped = linearToSrgb(neutralToneMap(color * uExposure));
            fragColor = vec4(mix(background, mapped, a), 1.0);
        }
        """;
}
