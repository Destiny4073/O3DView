#version 430 core
layout (location = 0) in vec3 pos;
layout (location = 1) in vec4 col;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;

out vec4 vCol;
out vec3 vPos;

void main()
{
    vec4 worldPos = uModel * vec4(pos, 1.0);
    vec4 viewPos = uView * worldPos;
    gl_Position = uProj * viewPos;
    vCol = col;
    vPos = worldPos.xyz;
}
