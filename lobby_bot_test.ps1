param(
    [int]$BotSayisi = 4,
    [int]$PollSeconds = 2,
    [int]$MaxWaitSeconds = 120,
    [string]$LobbyId = ""       # bos birakilirsa otomatik bulur
)

$ApiKey = "AIzaSyDIVzy4-HXXseudNlzQttP7wlZlTyrZCdE"
$RtdbUrl = "https://klyzegg-default-rtdb.firebaseio.com"
$AuthUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$ApiKey"

$BotIsimleri = @("Phoenix", "Jett", "Reyna", "Raze", "Sage", "Cypher", "Brimstone", "Viper", "Omen", "Killjoy")
$BotEtiketleri = @("TR1", "EUW", "NA1", "BR2", "KR3", "APAC", "LATAM", "ME1")
$BotRutbeleri = @("Demir", "Bronz", "Gümüs", "Altin", "Platin", "Elmas", "Ölümsüz", "Radyant")

function BotAdi($i) { return $BotIsimleri[$i % $BotIsimleri.Length] + $i }
function BotTag($i) { return $BotEtiketleri[$i % $BotEtiketleri.Length] }
function BotElo($i) { return 800 + ($i * 150) }
function BotTier($i) { return [Math]::Max(1, [Math]::Min(8, 3 + ($i % 5))) }
function BotRank($i) { return $BotRutbeleri[([Math]::Max(0, [Math]::Min(7, 2 + ($i % 5))))] }

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "    KLYZE LOBI BOT TEST (4 Bot)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/5] Firebase kimlik dogrulama..." -NoNewline
try {
    $authBody = @{ returnSecureToken = $true } | ConvertTo-Json
    $authResp = Invoke-RestMethod -Uri $AuthUrl -Method Post -Body $authBody -ContentType "application/json"
    $idToken = $authResp.idToken
    Write-Host " OK (Token: $($idToken.Substring(0,10))...)" -ForegroundColor Green
} catch {
    Write-Host " HATA: $_" -ForegroundColor Red
    exit 1
}

$totalWait = 0
$lobby = $null
$lobbyKey = ""

# Lobi bulma döngüsü
while ($totalWait -lt $MaxWaitSeconds) {
    Write-Host "[2/5] Lobi araniyor... (${totalWait}s / ${MaxWaitSeconds}s)" -NoNewline
    try {
        $rooms = Invoke-RestMethod -Uri "$RtdbUrl/rooms.json?auth=$idToken" -Method Get
    } catch {
        Write-Host " HATA" -ForegroundColor Red
        Start-Sleep -Seconds $PollSeconds
        $totalWait += $PollSeconds
        continue
    }

    if ($rooms -eq $null) {
        Write-Host " - Lobi yok" -ForegroundColor Yellow
        Start-Sleep -Seconds $PollSeconds
        $totalWait += $PollSeconds
        continue
    }

    if ($LobbyId -ne "") {
        if ($rooms.PSObject.Properties.Name -contains "l_$LobbyId") {
            $lobbyKey = "l_$LobbyId"
            $lobby = $rooms.$lobbyKey
            Write-Host " - Belirtilen lobi bulundu" -ForegroundColor Green
            break
        }
        Write-Host " - Lobi (l_$LobbyId) bulunamadi" -ForegroundColor Yellow
        Start-Sleep -Seconds $PollSeconds
        $totalWait += $PollSeconds
        continue
    }

    # Otomatik bul: waiting ve dolu olmayan lobiler
    $found = $null; $foundKey = $null
    $rooms.PSObject.Properties | Where-Object { $_.Name -like "l_*" } | ForEach-Object {
        $v = $_.Value
        $oyuncuSayisi = @($v.players).Count
        if ($v.status -eq "waiting" -and $oyuncuSayisi -lt $v.maxPlayers) {
            $found = $v; $foundKey = $_.Name
        }
    }

    if ($found -ne $null) {
        $lobby = $found; $lobbyKey = $foundKey
        Write-Host " - Bulundu: $($lobby.hostName)#$($lobby.hostTag) ($(@($lobby.players).Count)/$($lobby.maxPlayers))" -ForegroundColor Green
        break
    }

    Write-Host " - Uygun lobi yok" -ForegroundColor Yellow
    Start-Sleep -Seconds $PollSeconds
    $totalWait += $PollSeconds
}

if ($lobby -eq $null) {
    Write-Host ""
    Write-Host "Zaman asimi: Lobi bulunamadi ($MaxWaitSeconds sn)." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== LOBI BULUNDU ===" -ForegroundColor Cyan
Write-Host "  ID:        $lobbyKey"
Write-Host "  Host:      $($lobby.hostName)#$($lobby.hostTag)"
Write-Host "  Mod:       $($lobby.gameMode)"
Write-Host "  Oyuncular: $(@($lobby.players).Count)/$($lobby.maxPlayers)"
Write-Host "  Grup Kodu: $($lobby.groupCode)"
Write-Host ""

# Varolan oyuncu listesini al
function Get-PlayerList($lobbyData) {
    $list = @()
    if ($lobbyData.players -eq $null) { return $list }
    foreach ($p in @($lobbyData.players)) {
        if ($p -ne $null) {
            $list += @{
                name = "$($p.name)"
                tag  = "$($p.tag)"
                elo  = [int]($p.elo -as [int])
                tier = [int]($p.tier -as [int])
                rank = if ($p.rank) { "$($p.rank)" } else { "" }
            }
        }
    }
    return $list
}

# Bot ekleme
for ($i = 0; $i -lt $BotSayisi; $i++) {
    Write-Host "[3/5] Bot $($i+1)/$BotSayisi ekleniyor..." -NoNewline

    try {
        # Lobi son durumunu al
        $lobbyCurrent = Invoke-RestMethod -Uri "$RtdbUrl/rooms/$lobbyKey.json?auth=$idToken" -Method Get
        $oyuncular = Get-PlayerList($lobbyCurrent)
        $max = if ($lobbyCurrent.maxPlayers) { [int]$lobbyCurrent.maxPlayers } else { 5 }

        if ($oyuncular.Count -ge $max) {
            Write-Host " Lobi dolu, bot eklenemiyor" -ForegroundColor Yellow
            break
        }

        $botAdi = BotAdi($i)
        $botTag = BotTag($i)

        # Bot zaten var mi kontrol et
        $varMi = $oyuncular | Where-Object { $_.name -eq $botAdi -and $_.tag -eq $botTag }
        if ($varMi) {
            Write-Host " Zaten var, atlanıyor" -ForegroundColor Yellow
            continue
        }

        $botPlayer = @{
            name = $botAdi
            tag  = $botTag
            elo  = BotElo($i)
            tier = BotTier($i)
            rank = BotRank($i)
        }

        $yeniOyuncular = $oyuncular + $botPlayer
        $yeniDurum = if ($yeniOyuncular.Count -ge $max) { "full" } else { "waiting" }

        $patchBody = @{ players = @($yeniOyuncular); status = $yeniDurum } | ConvertTo-Json -Depth 10
        Invoke-RestMethod -Uri "$RtdbUrl/rooms/$lobbyKey.json?auth=$idToken" -Method Patch -Body $patchBody -ContentType "application/json"

        Write-Host " EKLENDI -> $botAdi#$botTag ($(@($yeniOyuncular).Count)/$max - $yeniDurum)" -ForegroundColor Green

        if ($yeniDurum -eq "full") {
            Write-Host ""
            Write-Host "=== LOBI DOLDU! ===" -ForegroundColor Green
            Write-Host "Grup Kodu: $($lobbyCurrent.groupCode)" -ForegroundColor Yellow
            Write-Host "Oyuncular:"
            $yeniOyuncular | ForEach-Object { Write-Host "  - $($_.name)#$($_.tag) Elo:$($_.elo)" }
            Write-Host ""
            Write-Host "[4/5] Tamamlandi - $($BotSayisi) bot eklendi" -ForegroundColor Green
            exit 0
        }

        Start-Sleep -Seconds 1.5
    } catch {
        Write-Host " HATA: $_" -ForegroundColor Red
        # Devam et
    }
}

Write-Host ""
Write-Host "[5/5] $($i) bot eklendi, lobby bekliyor (status: $yeniDurum)." -ForegroundColor Green
Write-Host "Grup Kodu: $($lobby.groupCode)" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
