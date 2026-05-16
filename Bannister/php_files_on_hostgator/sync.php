<?php
/**
 * Bannister Sync Endpoint  (multi-user + operation queue version)
 * Deployed at: /home1/nadavrock/public_html/bannister/sync.php
 * Public URL:  https://nadavrock.com/bannister/sync.php
 *
 * Routes:
 *   GET                       - Download user's database
 *   GET  ?action=info         - DB metadata
 *   POST                      - Upload user's database
 *   POST ?action=register     - Register a new user
 *   POST ?action=queue_upload - Upload pending operations from a secondary device
 *   GET  ?action=queue_download - Download merged pending operations (for master)
 *   POST ?action=queue_clear  - Mark applied operation UUIDs as cleared
 */

if (empty($_SERVER['HTTPS']) || $_SERVER['HTTPS'] === 'off') {
    http_response_code(400);
    header('Content-Type: application/json');
    echo json_encode(['error' => 'HTTPS required']);
    exit;
}

$STORAGE_ROOT     = '/home1/nadavrock/bannister_data';
$USERS_FILE       = $STORAGE_ROOT . '/users.json';
$MAX_UPLOAD_BYTES = 100 * 1024 * 1024;
$MAX_QUEUE_BYTES  = 5 * 1024 * 1024;
$KEEP_BACKUPS     = 3;

$USERNAME_REGEX   = '/^[a-z0-9_\-]{3,30}$/';
$DEVICE_ID_REGEX  = '/^[a-zA-Z0-9\-]{8,64}$/';

function fail($code, $msg)
{
    http_response_code($code);
    header('Content-Type: application/json');
    echo json_encode(['error' => $msg]);
    exit;
}

function ok($payload)
{
    header('Content-Type: application/json');
    echo json_encode($payload);
    exit;
}

function load_users(string $usersFile): array
{
    if (!file_exists($usersFile)) return [];
    $fp = fopen($usersFile, 'r');
    if (!$fp) fail(500, 'Failed to open users file');
    if (!flock($fp, LOCK_SH)) { fclose($fp); fail(500, 'Failed to lock users file'); }
    $contents = stream_get_contents($fp);
    flock($fp, LOCK_UN);
    fclose($fp);
    if ($contents === false || trim($contents) === '') return [];
    $data = json_decode($contents, true);
    return is_array($data) ? $data : [];
}

function save_users(string $usersFile, array $users): void
{
    $dir = dirname($usersFile);
    if (!is_dir($dir) && !mkdir($dir, 0700, true)) fail(500, 'Failed to create storage root');
    $tmp = $usersFile . '.tmp';
    $fp = fopen($tmp, 'w');
    if (!$fp) fail(500, 'Failed to open users file for write');
    if (!flock($fp, LOCK_EX)) { fclose($fp); @unlink($tmp); fail(500, 'Failed to lock users file'); }
    fwrite($fp, json_encode($users, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES));
    fflush($fp);
    flock($fp, LOCK_UN);
    fclose($fp);
    if (!rename($tmp, $usersFile)) { @unlink($tmp); fail(500, 'Failed to install users file'); }
}

function sanitize_username(string $raw): string { return strtolower(trim($raw)); }
function valid_username(string $u): bool { global $USERNAME_REGEX; return preg_match($USERNAME_REGEX, $u) === 1; }
function valid_hash(string $h): bool {
    if (strlen($h) < 40 || strlen($h) > 64) return false;
    return preg_match('/^[A-Za-z0-9+\/=]+$/', $h) === 1;
}
function valid_device_id(string $id): bool { global $DEVICE_ID_REGEX; return preg_match($DEVICE_ID_REGEX, $id) === 1; }

function authenticate(string $usersFile): string
{
    $hdr = $_SERVER['HTTP_X_AUTH'] ?? '';
    if ($hdr === '') fail(401, 'Missing X-Auth header');
    $parts = explode(':', $hdr, 2);
    if (count($parts) !== 2) fail(401, 'Malformed X-Auth header');
    $user = sanitize_username($parts[0]);
    $hash = $parts[1];
    if (!valid_username($user)) fail(401, 'Invalid username');
    $users = load_users($usersFile);
    if (!isset($users[$user])) fail(401, 'Unknown user');
    if (!hash_equals($users[$user]['hash'], $hash)) fail(401, 'Invalid credentials');
    $users[$user]['last_seen'] = time();
    try { save_users($usersFile, $users); } catch (Throwable $e) { /* ignore */ }
    return $user;
}

function user_dir(string $root, string $user): string
{
    $dir = "$root/$user";
    if (!is_dir($dir) && !mkdir($dir, 0700, true)) fail(500, 'Failed to create storage directory');
    return $dir;
}

function db_path(string $root, string $user): string
{
    return user_dir($root, $user) . '/bannister.db';
}

function queue_path(string $root, string $user, string $deviceId): string
{
    return user_dir($root, $user) . '/operation_queue_' . $deviceId . '.json';
}

function write_json_atomic(string $path, array $data): void
{
    $tmp = $path . '.tmp';
    $fp = fopen($tmp, 'w');
    if (!$fp) fail(500, 'Failed to open queue file for write');
    if (!flock($fp, LOCK_EX)) { fclose($fp); @unlink($tmp); fail(500, 'Failed to lock queue file'); }
    fwrite($fp, json_encode($data, JSON_UNESCAPED_SLASHES));
    fflush($fp);
    flock($fp, LOCK_UN);
    fclose($fp);
    if (!rename($tmp, $path)) { @unlink($tmp); fail(500, 'Failed to install queue file'); }
}

function read_json(string $path): ?array
{
    if (!file_exists($path)) return null;
    $fp = fopen($path, 'r');
    if (!$fp) return null;
    if (!flock($fp, LOCK_SH)) { fclose($fp); return null; }
    $contents = stream_get_contents($fp);
    flock($fp, LOCK_UN);
    fclose($fp);
    if ($contents === false || trim($contents) === '') return null;
    $data = json_decode($contents, true);
    return is_array($data) ? $data : null;
}

$method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
$action = $_GET['action'] ?? '';

if ($method === 'POST' && $action === 'register') {
    $raw = file_get_contents('php://input');
    if ($raw === false || $raw === '') fail(400, 'Empty request body');
    $body = json_decode($raw, true);
    if (!is_array($body)) fail(400, 'Invalid JSON');
    $user = isset($body['username']) ? sanitize_username((string)$body['username']) : '';
    $hash = isset($body['hash']) ? (string)$body['hash'] : '';
    if (!valid_username($user)) fail(400, 'Invalid username. Must be 3-30 chars, lowercase letters, digits, underscore or hyphen.');
    if (!valid_hash($hash)) fail(400, 'Invalid hash format');
    $users = load_users($USERS_FILE);
    if (isset($users[$user])) fail(409, 'Username already taken');
    $users[$user] = ['hash' => $hash, 'created_at' => time(), 'last_seen' => time()];
    save_users($USERS_FILE, $users);
    user_dir($STORAGE_ROOT, $user);
    ok(['success' => true, 'username' => $user]);
}

$user = authenticate($USERS_FILE);

if ($method === 'POST' && $action === 'queue_upload') {
    $contentLength = (int)($_SERVER['CONTENT_LENGTH'] ?? 0);
    if ($contentLength > $MAX_QUEUE_BYTES) fail(413, 'Queue upload exceeds maximum size');
    $raw = file_get_contents('php://input');
    if ($raw === false || $raw === '') fail(400, 'Empty request body');
    $body = json_decode($raw, true);
    if (!is_array($body)) fail(400, 'Invalid JSON');
    $deviceId = isset($body['device_id']) ? (string)$body['device_id'] : '';
    if (!valid_device_id($deviceId)) fail(400, 'Invalid device_id');
    $operations = $body['operations'] ?? null;
    if (!is_array($operations)) fail(400, 'operations must be an array');
    $path = queue_path($STORAGE_ROOT, $user, $deviceId);
    write_json_atomic($path, [
        'device_id'   => $deviceId,
        'uploaded_at' => time(),
        'operations'  => $operations,
    ]);
    ok(['success' => true, 'uploaded_count' => count($operations)]);
}

if ($method === 'GET' && $action === 'queue_download') {
    $dir = user_dir($STORAGE_ROOT, $user);
    $files = glob("$dir/operation_queue_*.json") ?: [];
    $allOps = [];
    foreach ($files as $file) {
        $data = read_json($file);
        if ($data === null) continue;
        if (!isset($data['operations']) || !is_array($data['operations'])) continue;
        $basename = basename($file);
        $deviceId = preg_replace('/^operation_queue_(.+)\.json$/', '$1', $basename);
        foreach ($data['operations'] as $op) {
            if (!is_array($op)) continue;
            $op['device_id'] = $deviceId;
            $allOps[] = $op;
        }
    }
    usort($allOps, function ($a, $b) {
        $aTime = $a['created_at'] ?? '';
        $bTime = $b['created_at'] ?? '';
        return strcmp($aTime, $bTime);
    });
    ok(['operations' => $allOps]);
}

if ($method === 'POST' && $action === 'queue_clear') {
    $raw = file_get_contents('php://input');
    if ($raw === false || $raw === '') fail(400, 'Empty request body');
    $body = json_decode($raw, true);
    if (!is_array($body)) fail(400, 'Invalid JSON');
    $appliedUuids = $body['applied_uuids'] ?? null;
    if (!is_array($appliedUuids)) fail(400, 'applied_uuids must be an array');
    $appliedSet = array_flip(array_map('strval', $appliedUuids));
    $removedCount = 0;
    $dir = user_dir($STORAGE_ROOT, $user);
    $files = glob("$dir/operation_queue_*.json") ?: [];
    foreach ($files as $file) {
        $data = read_json($file);
        if ($data === null) continue;
        if (!isset($data['operations']) || !is_array($data['operations'])) continue;
        $remaining = [];
        foreach ($data['operations'] as $op) {
            $uuid = isset($op['uuid']) ? (string)$op['uuid'] : '';
            if ($uuid !== '' && isset($appliedSet[$uuid])) {
                $removedCount++;
                continue;
            }
            $remaining[] = $op;
        }
        if (count($remaining) === 0) {
            @unlink($file);
        } else {
            $data['operations'] = $remaining;
            write_json_atomic($file, $data);
        }
    }
    ok(['success' => true, 'removed_count' => $removedCount]);
}

$path = db_path($STORAGE_ROOT, $user);

if ($method === 'GET' && $action === 'info') {
    $exists = file_exists($path);
    ok([
        'exists'        => $exists,
        'size'          => $exists ? filesize($path) : 0,
        'last_modified' => $exists ? filemtime($path) : null,
    ]);
}

if ($method === 'GET') {
    if (!file_exists($path)) fail(404, 'No database has been uploaded yet');
    $size  = filesize($path);
    $mtime = filemtime($path);
    header('Content-Type: application/octet-stream');
    header('Content-Length: ' . $size);
    header('Last-Modified: ' . gmdate('D, d M Y H:i:s', $mtime) . ' GMT');
    header('ETag: "' . md5_file($path) . '"');
    header('Cache-Control: no-cache, no-store, must-revalidate');
    readfile($path);
    exit;
}

if ($method === 'POST') {
    $contentLength = (int)($_SERVER['CONTENT_LENGTH'] ?? 0);
    if ($contentLength <= 0) fail(400, 'Empty request body');
    if ($contentLength > $MAX_UPLOAD_BYTES) fail(413, 'Upload exceeds maximum size');
    $tmp = $path . '.tmp';
    $in  = fopen('php://input', 'rb');
    $out = fopen($tmp, 'wb');
    if (!$in || !$out) fail(500, 'Failed to open file streams');
    $written = 0;
    while (!feof($in)) {
        $chunk = fread($in, 65536);
        if ($chunk === false) break;
        $written += fwrite($out, $chunk);
        if ($written > $MAX_UPLOAD_BYTES) {
            fclose($in); fclose($out);
            @unlink($tmp);
            fail(413, 'Upload exceeds maximum size');
        }
    }
    fclose($in);
    fclose($out);
    if ($written === 0) { @unlink($tmp); fail(400, 'Empty upload'); }
    if (file_exists($path) && $KEEP_BACKUPS > 0) {
        for ($i = $KEEP_BACKUPS; $i >= 1; $i--) {
            $from = $path . '.bak.' . $i;
            $to   = $path . '.bak.' . ($i + 1);
            if ($i === $KEEP_BACKUPS && file_exists($to)) @unlink($to);
            if (file_exists($from)) @rename($from, $to);
        }
        @copy($path, $path . '.bak.1');
    }
    if (!rename($tmp, $path)) { @unlink($tmp); fail(500, 'Failed to install uploaded file'); }
    ok([
        'success'       => true,
        'size'          => $written,
        'last_modified' => filemtime($path),
    ]);
}

fail(405, 'Method not allowed');
