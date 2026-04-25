@echo off

:: echo azert van kikapcsolva, mert nagyon lelassitja a futast a bekapcsolt allapot (lassan ir a konzolra az op. rendszer).

chcp 1250 >nul
setlocal EnableDelayedExpansion
set errorlevel=

:: leellenorzi, hogy valoban ott van-e, ahonnan _betoltodott_ a program
for /F "usebackq tokens=*" %%I in (`cd`) do if /I "%%I\" NEQ "%~dp0" goto L_ERRCD
if errorlevel 1 goto L_ERRCD
goto L_NOERRCD
:L_ERRCD
echo "*** hiba: nem abban a mappaban fut a batch, amelybol betoltodott. ***"
echo "%~dp0"
for /F "usebackq tokens=*" %%I in (`cd`) do echo "Aktualis mappa: %%I"
echo "*** hiba: nem abban a mappaban fut a batch, amelybol betoltodott. ***" >del-bin-obj.cmd.log
echo "param-0: %~dp0" >>del-bin-obj.cmd.log
for /F "usebackq tokens=*" %%I in (`cd`) do echo "Aktualis mappa (cd kiemente): %%I" >>del-bin-obj.cmd.log
goto :EOF
:L_NOERRCD


:: csak akkor kezdi el a torlest, ha a fenti ellenorzesen ok.

for /r %%I in (bin, obj) do (
	rd /Q /S "%%I" >nul 2>&1 
)
