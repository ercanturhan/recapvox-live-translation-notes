$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$textPath = Join-Path $projectRoot 'samples\reference-en.txt'
$audioPath = Join-Path $projectRoot 'samples\reference-en.wav'
Add-Type -AssemblyName System.Speech
$voice = [System.Speech.Synthesis.SpeechSynthesizer]::new()
try {
    $english = $voice.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -eq 'en-US' } | Select-Object -First 1
    if (-not $english) { throw 'Windows üzerinde İngilizce TTS sesi bulunamadı.' }
    $voice.SelectVoice($english.VoiceInfo.Name)
    $voice.Rate = -1
    $audioFormat = [System.Speech.AudioFormat.SpeechAudioFormatInfo]::new(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $voice.SetOutputToWaveFile($audioPath, $audioFormat)
    $voice.Speak((Get-Content -LiteralPath $textPath -Raw -Encoding UTF8))
    $voice.SetOutputToNull()
    Write-Output $audioPath
}
finally { $voice.Dispose() }
