# DEV TOOL (not part of the app): a fake Sonarr (port 8989) and Radarr (port 7878) with a 300-item library and poster artwork,
# for trying the app without a real server. Run it and leave it running:
#     powershell -NoProfile -File tools/fake-arr/fake-arr.ps1
# From the Android emulator the servers are http://10.0.2.2:8989 (Sonarr) and http://10.0.2.2:7878 (Radarr); API key: goodkey.
# Every request is logged to tools/fake-arr/fake-arr.log (including the X-Api-Key header, to check it is never sent to the /remote/* "public" URLs).
# Hand-written from the documented v3 API, so it proves the app works with that shape, not that it matches a real server.
#
# Stand-in for Sonarr (8989) and Radarr (7878) on raw sockets, so any Host header works (the Android emulator sends 10.0.2.2).
# System status + library + artwork. Server artwork needs X-Api-Key: goodkey; /remote/* is the "public" fallback and must
# never receive the key (the log records the key header for every request so that can be checked).
Add-Type -AssemblyName System.Drawing

function New-Png($label, $c1, $c2) {
    $bmp = New-Object System.Drawing.Bitmap 400, 600
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $rect = New-Object System.Drawing.Rectangle 0, 0, 400, 600
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 70.0)
    $g.FillRectangle($brush, $rect)
    $font = New-Object System.Drawing.Font("Arial", 40, [System.Drawing.FontStyle]::Bold)
    $g.DrawString($label, $font, [System.Drawing.Brushes]::White, 30, 250)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return $ms.ToArray()
}
$serverPng = New-Png "SERVER" ([System.Drawing.Color]::FromArgb(255, 20, 80, 160)) ([System.Drawing.Color]::FromArgb(255, 120, 200, 255))
$remotePng = New-Png "REMOTE" ([System.Drawing.Color]::FromArgb(255, 160, 60, 20)) ([System.Drawing.Color]::FromArgb(255, 255, 200, 90))

$ports = @{ 8989 = @("Sonarr", "4.0.9.2244", "series"); 7878 = @("Radarr", "5.1.0.9360", "movie") }
$listeners = @{}
foreach ($p in $ports.Keys) {
    $l = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $p)
    $l.Start(); $listeners[$p] = $l
}
$log = "$PSScriptRoot\fake-arr.log"

function Library($port, $collection) {
    # 300 items so scrolling and virtualization can be checked. Each has its own poster URL (distinct cache keys).
    $items = New-Object System.Collections.Generic.List[string]
    for ($id = 1; $id -le 300; $id++) {
        $title = if ($id -eq 2) { "Remote Only With A Really Long Title That Must Truncate" } else { "Item {0:D3}" -f $id }
        $year = 1990 + ($id % 35)
        $day = "{0:D2}" -f (($id % 28) + 1)
        $monitored = if ($id % 7 -eq 0) { "false" } else { "true" }
        $poster = if ($id -eq 2) { "{`"coverType`":`"poster`",`"url`":`"/MediaCover/2/poster.jpg?lastWrite=1`",`"remoteUrl`":`"http://10.0.2.2:$port/remote/p2.png`"}" }
                  elseif ($id % 10 -eq 0) { "" }   # no art
                  else { "{`"coverType`":`"poster`",`"url`":`"/MediaCover/1/poster.jpg?lastWrite=$id`"}" }
        $images = if ($poster -eq "") { "[]" } else { "[$poster]" }
        if ($collection -eq "series") {
            $files = $id % 11; if ($files -gt 10) { $files = 10 }
            $items.Add("{`"id`":$id,`"title`":`"$title`",`"sortTitle`":`"$($title.ToLower())`",`"year`":$year,`"status`":`"continuing`",`"monitored`":$monitored,`"added`":`"2025-01-$day`T00:00:00Z`",`"images`":$images,`"statistics`":{`"seasonCount`":1,`"episodeFileCount`":$files,`"episodeCount`":10,`"totalEpisodeCount`":10,`"sizeOnDisk`":$($files * 1000)}}")
        } else {
            $hasFile = if ($id % 3 -eq 0) { "true" } else { "false" }
            $items.Add("{`"id`":$id,`"title`":`"$title`",`"sortTitle`":`"$($title.ToLower())`",`"year`":$year,`"status`":`"released`",`"monitored`":$monitored,`"hasFile`":$hasFile,`"isAvailable`":true,`"added`":`"2025-01-$day`T00:00:00Z`",`"images`":$images,`"sizeOnDisk`":$(if ($hasFile -eq 'true') { $id * 100 } else { 0 })}")
        }
    }
    "[" + ($items -join ",") + "]"
}

while ($true) {
    $idle = $true
    foreach ($p in $ports.Keys) {
        $l = $listeners[$p]
        if (-not $l.Pending()) { continue }
        $idle = $false
        $client = $l.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $stream.ReadTimeout = 2000
            $sb = New-Object System.Text.StringBuilder
            $buf = New-Object byte[] 4096
            while (-not $sb.ToString().Contains("`r`n`r`n")) {
                $n = $stream.Read($buf, 0, $buf.Length)
                if ($n -le 0) { break }
                [void]$sb.Append([Text.Encoding]::ASCII.GetString($buf, 0, $n))
            }
            $text = $sb.ToString()
            $path = if ($text -match '^GET (\S+)') { $Matches[1] } else { "" }
            $key = if ($text -match '(?im)^X-Api-Key:\s*(.+?)\s*$') { $Matches[1] } else { "" }
            $app, $version, $collection = $ports[$p]
            $type = "application/json"; $bytes = $null; $code = "200 OK"
            $bare = $path.Split('?')[0]

            if ($bare -like "/remote/*") { $type = "image/png"; $bytes = $remotePng }                       # public: no key needed
            elseif ($key -ne "goodkey") { $code = "401 Unauthorized"; $bytes = [Text.Encoding]::UTF8.GetBytes('{"error":"Unauthorized"}') }
            elseif ($bare -eq "/api/v3/system/status") { $bytes = [Text.Encoding]::UTF8.GetBytes("{`"appName`":`"$app`",`"version`":`"$version`",`"instanceName`":`"$app`"}") }
            elseif ($bare -eq "/api/v3/$collection") { $bytes = [Text.Encoding]::UTF8.GetBytes((Library $p $collection)) }
            elseif ($bare -eq "/MediaCover/1/poster.jpg") { $type = "image/png"; $bytes = $serverPng }
            else { $code = "404 Not Found"; $bytes = [Text.Encoding]::UTF8.GetBytes("{}") }

            $head = "HTTP/1.1 $code`r`nContent-Type: $type`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n`r`n"
            $hb = [Text.Encoding]::ASCII.GetBytes($head)
            $stream.Write($hb, 0, $hb.Length); $stream.Write($bytes, 0, $bytes.Length); $stream.Flush()
            "$(Get-Date -Format HH:mm:ss) :$p $path key=[$key] -> $code" | Out-File -Append $log
        } catch { "$(Get-Date -Format HH:mm:ss) :$p error $($_.Exception.Message)" | Out-File -Append $log }
        finally { $client.Close() }
    }
    if ($idle) { Start-Sleep -Milliseconds 50 }
}
