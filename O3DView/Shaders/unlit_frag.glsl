#version 430 core
in vec4 vCol;
in vec3 vPos;

out vec4 FragColor;

// Renderer
// uniform float uSelected;
// uniform vec3 uViewDir;
// uniform vec3 uViewPos;
// uniform float uTime;
// uniform float uFullbright;
// uniform float uWireframe;

void main()
{
    vec4 col = vCol;

    FragColor = col;
}
