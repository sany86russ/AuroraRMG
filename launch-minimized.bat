@echo off
rem ============================================================================
rem  Запуск генератора СВЁРНУТЫМ, не перехватывая фокус.
rem  Окно стартует в панели задач и НЕ выскакивает поверх полноэкранной игры
rem  (Dota и т.п.). Когда будете готовы — разверните его из панели задач.
rem
rem  Положите этот .bat рядом с OldenEraTemplateGenerator.exe и запускайте его
rem  вместо самого .exe.
rem ----------------------------------------------------------------------------
rem  Launch the generator MINIMIZED without stealing focus, so it never pops
rem  over a fullscreen game. Place next to OldenEraTemplateGenerator.exe.
rem ============================================================================
start "" "%~dp0OldenEraTemplateGenerator.exe" --minimized
