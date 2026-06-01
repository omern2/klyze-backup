<?php
header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST');
header('Access-Control-Allow-Headers: Content-Type');

$file = 'downloads.json';

// Dosya yoksa oluştur
if (!file_exists($file)) {
    $data = ['count' => 0, 'lastUpdate' => date('c')];
    file_put_contents($file, json_encode($data));
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    // İndirme sayısını oku
    $data = json_decode(file_get_contents($file), true);
    echo json_encode($data);
    
} elseif ($_SERVER['REQUEST_METHOD'] === 'POST') {
    // İndirme sayısını artır
    $data = json_decode(file_get_contents($file), true);
    $data['count'] = isset($data['count']) ? $data['count'] + 1 : 1;
    $data['lastUpdate'] = date('c');
    
    file_put_contents($file, json_encode($data));
    echo json_encode($data);
}
?>
