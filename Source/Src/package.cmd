@ECHO OFF
SETLOCAL
SET SOLUTIONPATH=%~dp0..
SET PRIMETIME_PROJECTPATH=%SOLUTIONPATH%\Src\KZDev.PrimeTime
SET SYSTEMCLOCK_PROJECTPATH=%SOLUTIONPATH%\Src\KZDev.SystemClock.PrimeTime
SET PRIMETIME_TESTING_PROJECTPATH=%SOLUTIONPATH%\Src\Testing\KZDev.PrimeTime.Testing
SET SYSTEMCLOCK_TESTING_PROJECTPATH=%SOLUTIONPATH%\Src\Testing\KZDev.SystemClock.PrimeTime.Testing

PUSHD %SOLUTIONPATH%

dotnet clean -c Release
dotnet restore

PUSHD %PRIMETIME_PROJECTPATH%
dotnet pack -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
POPD

PUSHD %SYSTEMCLOCK_PROJECTPATH%
dotnet pack -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
POPD

PUSHD %PRIMETIME_TESTING_PROJECTPATH%
dotnet pack -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
POPD

PUSHD %SYSTEMCLOCK_TESTING_PROJECTPATH%
dotnet pack -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true

POPD
POPD

:EOF
ENDLOCAL
