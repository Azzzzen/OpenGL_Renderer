cmake_minimum_required(VERSION 3.10)

# 设置项目名称和语言
project(OpenGLProject LANGUAGES CXX)

# 设置 C++ 标准
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)

set(glfw3_DIR "C:/Users/Admin/vcpkg/installed/x64-windows/share/glfw3")
set(glm_DIR "C:/Users/Admin/vcpkg/installed/x64-windows/share/glm")
set(glad_DIR "C:/Users/Admin/vcpkg/installed/x64-windows/share/glad")
set(assimp_DIR "C:/Users/Admin/vcpkg/installed/x64-windows/share/assimp")
set(pugixml_DIR "C:/Users/Admin/vcpkg/installed/x64-windows/share/pugixml")

find_package(glm CONFIG REQUIRED)
find_package(glfw3 CONFIG REQUIRED)
find_package(glad CONFIG REQUIRED)
find_package(assimp CONFIG REQUIRED)
find_package(pugixml CONFIG REQUIRED)

# 添加可执行文件
add_executable(OpenGLProject
    opengl.cpp
)

# 设置头文件搜索路径
target_include_directories(OpenGLProject PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
)

# 链接库文件
target_link_libraries(OpenGLProject PRIVATE
    glm::glm
    glfw
    glad::glad
    assimp::assimp
    pugixml
)