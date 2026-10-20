$encKey = [Convert]::FromBase64String('+Ry5j92i/WOUkeHKazmyf3QanmWf96qNZXINbIRscys=')
$authKey = [Convert]::FromBase64String('Oz+JiHUWzsv6NVASzQRD5URhjpHpVQCIszVhBi08fOE=')
$bytes = [System.IO.File]::ReadAllBytes('appsettings.enc')

$hmac = [System.Security.Cryptography.HMACSHA256]::new($authKey)
$computed = $hmac.ComputeHash($bytes, 0, $bytes.Length - 32)
$stored = $bytes[($bytes.Length - 32)..($bytes.Length - 1)]

$match = [System.Linq.Enumerable]::SequenceEqual([byte[]]$computed, [byte[]]$stored)
Write-Output "HMAC matches: $match"

if ($match) {
    $iv = $bytes[5..20]
    $cipher = [System.Security.Cryptography.Aes]::Create()
    $cipher.Key = $encKey
    $cipher.IV = $iv
    $cipher.Mode = [System.Security.Cryptography.CipherMode]::CBC
    $cipher.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
    $decryptor = $cipher.CreateDecryptor()
    $payload = $bytes[21..($bytes.Length - 33)]
    $plain = $decryptor.TransformFinalBlock($payload, 0, $payload.Length)
    $text = [System.Text.Encoding]::UTF8.GetString($plain)
    Write-Output "Decrypted JSON content:"
    Write-Output $text
    [System.IO.File]::WriteAllText('appsettings.decrypted.json', $text)
    Write-Output "Saved to appsettings.decrypted.json"
}
