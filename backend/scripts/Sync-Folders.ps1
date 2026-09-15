param(
    [Parameter(Mandatory)]
    [string]$Source,

    [Parameter(Mandatory)]
    [string]$Dest,

    [switch]$Preview
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Исходная папка не найдена: $Source"
}

$Source = (Get-Item -LiteralPath $Source).FullName
$Dest = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Dest)

if (Test-Path -LiteralPath $Dest -PathType Leaf) {
    throw "Путь назначения указывает на файл: $Dest"
}

# Запрещаем одинаковые и вложенные друг в друга папки.
$sourcePrefix = $Source.TrimEnd('\') + '\'
$destPrefix = $Dest.TrimEnd('\') + '\'
$comparison = [StringComparison]::OrdinalIgnoreCase

if ($sourcePrefix.StartsWith($destPrefix, $comparison) -or
    $destPrefix.StartsWith($sourcePrefix, $comparison)) {
    throw "Source и Dest должны быть разными, невложенными папками."
}

$options = @(
    '/E'         # Все подпапки, включая пустые
    '/PURGE'     # Удалять отсутствующие в source
    '/XO'        # Не копировать более старые файлы
    '/XC'        # Не копировать при равной дате и разном размере
    '/COPY:DAT'  # Данные, атрибуты, дата изменения
    '/XJ'        # Не обходить junction points
    '/R:2'       # Две повторные попытки при ошибке
    '/W:1'       # Интервал между попытками — одна секунда
)

if ($Preview) {
    $options += '/L' # Только показать план изменений
}

& robocopy.exe $Source $Dest @options
$result = $LASTEXITCODE

# Коды robocopy от 0 до 7 не означают ошибку копирования.
if ($result -ge 8) {
    throw "Robocopy завершился с ошибкой. Код: $result"
}
