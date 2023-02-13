$ProgressPreference = 'Continue'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$erlang_reg_path = 'HKLM:\SOFTWARE\Ericsson\Erlang'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $erlang_reg_path = 'HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang'
}
$erlang_erts_version = Get-ChildItem -Path $erlang_reg_path -Name
$erlang_home = (Get-ItemProperty -LiteralPath $erlang_reg_path\$erlang_erts_version).'(default)'

Write-Host "[INFO] Setting ERLANG_HOME to '$erlang_home'..."
$env:ERLANG_HOME = $erlang_home
[Environment]::SetEnvironmentVariable('ERLANG_HOME', $erlang_home, 'Machine')

$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $regPath = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
}

$rabbitmq_base_path = Split-Path -Parent (Get-ItemProperty $regPath 'UninstallString').UninstallString
$rabbitmq_version = (Get-ItemProperty $regPath "DisplayVersion").DisplayVersion
$rabbitmqctl_path = Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version" | Join-Path -ChildPath 'sbin' | Join-Path -ChildPath 'rabbitmqctl.bat'

Write-Host "[INFO] Setting RABBITMQ_RABBITMQCTL_PATH to '$rabbitmqctl_path'..."
$env:RABBITMQ_RABBITMQCTL_PATH = $rabbitmqctl_path
[Environment]::SetEnvironmentVariable('RABBITMQ_RABBITMQCTL_PATH', $rabbitmqctl_path, 'Machine')

$solution_file = Join-Path -Path $env:GITHUB_WORKSPACE -ChildPath 'RabbitMQDotNetClient.sln'
dotnet test --no-restore --no-build --logger "console;verbosity=detailed" $solution_file
