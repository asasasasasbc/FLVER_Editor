echo off
cd /d %~dp0
havok_toolset hk_to_gltf %*
if %errorlevel% NEQ 0 pause
