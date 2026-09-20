cmake_minimum_required(VERSION 3.20)
project(ReelForge.Native LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(ReelForge.Native SHARED
    src/ReelForgeNative.cpp
    include/ReelForgeNative.h
)

target_compile_definitions(ReelForge.Native PRIVATE UNICODE _UNICODE)
