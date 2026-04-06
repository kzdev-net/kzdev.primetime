@ECHO OFF
SETLOCAL
SET SOLUTIONPATH=%~dp0..
SET PROJECTPATH=%SOLUTIONPATH%Src\KZDev.PrimeTime

PUSHD %SOLUTIONPATH%

dotnet clean -c Release
dotnet restore

PUSHD %PROJECTPATH%
 
dotnet pack -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true

POPD
POPD

:EOF
ENDLOCAL
