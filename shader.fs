#version 410 core
out vec4 FragColor;

in vec3 Normal;  
in vec3 FragPos;  
in vec2 TexCoords;

struct Material {
    sampler2D diffuse;
    sampler2D specular;
    float shininess;
};

uniform vec3 lightColor;         
uniform vec3 lightPos;              
uniform vec3 viewPos;              
uniform Material material;
uniform sampler2D texture_diffuse1;
uniform sampler2D texture_specular1;

void main()
{
  // 计算环境光 (Ambient)
    vec3 ambient = 0.1 * lightColor;

    // 计算光源方向
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    
    // 漫反射光 (Diffuse)
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * lightColor;

    // 计算视角方向
    vec3 viewDir = normalize(viewPos - FragPos);

    // 镜面反射光 (Specular)
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 1.0f);
    vec3 specular = spec * lightColor;

    // 合成最终颜色
    vec3 result = ambient + diffuse + specular;

    // 输出最终的片段颜色
    FragColor = vec4(result, 1.0)*texture(texture_diffuse1, TexCoords);
}