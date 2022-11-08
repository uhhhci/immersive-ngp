Shader "VolumeRendering/RGBAVolumeRenderingShader"
{
    Properties
    {
        _DataTextRGBA("Data Texture RGBA (Generated)", 3D) = "" {}
        _NoiseTex("Noise Texture (Generated)", 2D) = "white" {}
        _MinVal("Min val", Range(0.0, 1.0)) = 0.0
        _MaxVal("Max val", Range(0.0, 1.0)) = 1.0
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100
            Cull Front
            ZTest LEqual
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            Pass
            {
                CGPROGRAM
                #pragma multi_compile MODE_DVR_RGBA
                #pragma multi_compile __ TF2D_ON
                #pragma multi_compile __ CUTOUT_PLANE CUTOUT_BOX_INCL CUTOUT_BOX_EXCL
                #pragma multi_compile __ LIGHTING_ON
                #pragma multi_compile DEPTHWRITE_ON DEPTHWRITE_OFF
                #pragma multi_compile __ DVR_BACKWARD_ON
                #pragma multi_compile __ RAY_TERMINATE_ON
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                #define CUTOUT_ON CUTOUT_PLANE || CUTOUT_BOX_INCL || CUTOUT_BOX_EXCL

                struct vert_in
                {
                    float4 vertex : POSITION;
                    float4 normal : NORMAL;
                    float2 uv : TEXCOORD0;
                };

                struct frag_in
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float3 vertexLocal : TEXCOORD1;
                    float3 normal : NORMAL;
                };

                struct frag_out
                {
                    float4 colour : SV_TARGET;
    #if DEPTHWRITE_ON
                    float depth : SV_DEPTH;
    #endif
                };

                sampler3D _DataTextRGBA;
                sampler2D _NoiseTex;

                float _MinVal;
                float _MaxVal;

    #if CUTOUT_ON
                float4x4 _CrossSectionMatrix;
    #endif

                struct RayInfo
                {
                    float3 startPos;
                    float3 endPos;
                    float3 direction;
                    float2 aabbInters;
                };

                struct RaymarchInfo
                {
                    RayInfo ray;
                    int numSteps;
                    float numStepsRecip;
                    float stepSize;
                };

                float3 getViewRayDir(float3 vertexLocal)
                {
                    if (unity_OrthoParams.w == 0)
                    {
                        // Perspective
                        return normalize(ObjSpaceViewDir(float4(vertexLocal, 0.0f)));
                    }
                    else
                    {
                        // Orthographic
                        float3 camfwd = mul((float3x3)unity_CameraToWorld, float3(0,0,-1));
                        float4 camfwdobjspace = mul(unity_WorldToObject, camfwd);
                        return normalize(camfwdobjspace);
                    }
                }

                // Find ray intersection points with axis aligned bounding box
                float2 intersectAABB(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
                {
                    float3 tMin = (boxMin - rayOrigin) / rayDir;
                    float3 tMax = (boxMax - rayOrigin) / rayDir;
                    float3 t1 = min(tMin, tMax);
                    float3 t2 = max(tMin, tMax);
                    float tNear = max(max(t1.x, t1.y), t1.z);
                    float tFar = min(min(t2.x, t2.y), t2.z);
                    return float2(tNear, tFar);
                };

                // Get a ray for the specified fragment (back-to-front)
                RayInfo getRayBack2Front(float3 vertexLocal)
                {
                    RayInfo ray;
                    ray.direction = getViewRayDir(vertexLocal);
                    ray.startPos = vertexLocal + float3(0.5f, 0.5f, 0.5f);
                    // Find intersections with axis aligned boundinng box (the volume)
                    ray.aabbInters = intersectAABB(ray.startPos, ray.direction, float3(0.0, 0.0, 0.0), float3(1.0f, 1.0f, 1.0));

                    // Check if camera is inside AABB
                    const float3 farPos = ray.startPos + ray.direction * ray.aabbInters.y - float3(0.5f, 0.5f, 0.5f);
                    float4 clipPos = UnityObjectToClipPos(float4(farPos, 1.0f));
                    ray.aabbInters += min(clipPos.w, 0.0);

                    ray.endPos = ray.startPos + ray.direction * ray.aabbInters.y;
                    return ray;
                }

                // Get a ray for the specified fragment (front-to-back)
                RayInfo getRayFront2Back(float3 vertexLocal)
                {
                    RayInfo ray = getRayBack2Front(vertexLocal);
                    ray.direction = -ray.direction;
                    float3 tmp = ray.startPos;
                    ray.startPos = ray.endPos;
                    ray.endPos = tmp;
                    return ray;
                }

                RaymarchInfo initRaymarch(RayInfo ray, int maxNumSteps)
                {
                    RaymarchInfo raymarchInfo;
                    raymarchInfo.stepSize = 1.732f/*greatest distance in box*/ / maxNumSteps;
                    raymarchInfo.numSteps = (int)clamp(abs(ray.aabbInters.x - ray.aabbInters.y) / raymarchInfo.stepSize, 1, maxNumSteps);
                    raymarchInfo.numStepsRecip = 1.0 / raymarchInfo.numSteps;
                    return raymarchInfo;
                }

                float4 BlendUnder(float4 color, float4 newColor)
                {
                    color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                    color.a += (1.0 - color.a) * newColor.a;
                    return color;
                }


                float4 getRGBAColor(float3 pos) {

                    return tex3Dlod(_DataTextRGBA, float4(pos.x, pos.y, pos.z, 0.0f)).rgba;
                }

                // Performs lighting calculations, and returns a modified colour.
                float3 calculateLighting(float3 col, float3 normal, float3 lightDir, float3 eyeDir, float specularIntensity)
                {
                    float ndotl = max(lerp(0.0f, 1.5f, dot(normal, lightDir)), 0.5f); // modified, to avoid volume becoming too dark
                    float3 diffuse = ndotl * col;
                    float3 v = eyeDir;
                    float3 r = normalize(reflect(-lightDir, normal));
                    float rdotv = max(dot(r, v), 0.0);
                    float3 specular = pow(rdotv, 32.0f) * float3(1.0f, 1.0f, 1.0f) * specularIntensity;
                    return diffuse + specular;
                }

                // Converts local position to depth value
                float localToDepth(float3 localPos)
                {
                    float4 clipPos = UnityObjectToClipPos(float4(localPos, 1.0f));

    #if defined(SHADER_API_GLCORE) || defined(SHADER_API_OPENGL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    return (clipPos.z / clipPos.w) * 0.5 + 0.5;
    #else
                    return clipPos.z / clipPos.w;
    #endif
                }

                bool IsCutout(float3 currPos)
                {
    #if CUTOUT_ON
                    // Move the reference in the middle of the mesh, like the pivot
                    float3 pos = currPos - float3(0.5f, 0.5f, 0.5f);

                    // Convert from model space to plane's vector space
                    float3 planeSpacePos = mul(_CrossSectionMatrix, float4(pos, 1.0f));

        #if CUTOUT_PLANE
                    return planeSpacePos.z > 0.0f;
        #elif CUTOUT_BOX_INCL
                    return !(planeSpacePos.x >= -0.5f && planeSpacePos.x <= 0.5f && planeSpacePos.y >= -0.5f && planeSpacePos.y <= 0.5f && planeSpacePos.z >= -0.5f && planeSpacePos.z <= 0.5f);
        #elif CUTOUT_BOX_EXCL
                    return planeSpacePos.x >= -0.5f && planeSpacePos.x <= 0.5f && planeSpacePos.y >= -0.5f && planeSpacePos.y <= 0.5f && planeSpacePos.z >= -0.5f && planeSpacePos.z <= 0.5f;
        #endif
    #else
                    return false;
    #endif
                }

                frag_in vert_main(vert_in v)
                {
                    frag_in o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    o.vertexLocal = v.vertex;
                    o.normal = UnityObjectToWorldNormal(v.normal);
                    return o;
                }


                frag_out frag_dvr_rgba(frag_in i) {

    #define MAX_NUM_STEPS 128
                    // Allowed floating point inaccuracy
                    #define EPSILON 0.00001f

                                    RayInfo ray = getRayFront2Back(i.vertexLocal);
                                    RaymarchInfo raymarchInfo = initRaymarch(ray, MAX_NUM_STEPS);


                                    // Create a small random offset in order to remove artifacts
                                    ray.startPos = ray.startPos + (2.0f * ray.direction * raymarchInfo.stepSize) * tex2D(_NoiseTex, float2(i.uv.x, i.uv.y)).r;

                                    float4 col = float4(0, 0, 0, 0);

                                    float3 rayOrigin = i.vertexLocal;
                                    //// Use vector from camera to object surface to get ray direction
                                    float3 rayDirection = ray.direction; //mul(unity_WorldToObject, float4(normalize(i.vectorToSurface), 1));

                                    //float4 color = float4(0, 0, 0, 0);
                                    float3 samplePosition = rayOrigin;


                                    for (int iStep = 0; iStep < raymarchInfo.numSteps; iStep++)
                                    {
                                        const float t = iStep * raymarchInfo.numStepsRecip;
                                        const float3 currPos = lerp(ray.startPos, ray.endPos, t);

                    #ifdef CUTOUT_ON
                                        if (IsCutout(currPos))
                                            continue;
                    #endif

                                        //const float density = getDensity(currPos);
                                        float4 rgba = getRGBAColor(currPos);
                                        const float density = rgba.a;

                                        if (density > _MinVal && density < _MaxVal)
                                        {

                                            col.rgba = getRGBAColor(currPos);
                                            //col.a = density;
                                            break;
                                        }
                                        else {
                                            col.a = 0;
                                        }
                                    }

                                    // Write fragment output
                                    frag_out output;
                                    output.colour = col;
                    #if DEPTHWRITE_ON

                                    const float tDepth = iStep * raymarchInfo.numStepsRecip + (step(col.a, 0.0) * 1000.0); // Write large depth if no hit
                                    output.depth = localToDepth(lerp(ray.startPos, ray.endPos, tDepth) - float3(0.5f, 0.5f, 0.5f));
                    #endif
                                    return output;
                                }

                                frag_in vert(vert_in v)
                                {
                                    return vert_main(v);
                                }

                                frag_out frag(frag_in i)
                                {
                                    return frag_dvr_rgba(i);

                                }

                                ENDCG
                            }
        }
}
