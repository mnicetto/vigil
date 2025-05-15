<?php
header('Content-Type: application/json');

$logFile = __DIR__ . '/log.txt';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $jsonPayload = file_get_contents('php://input');

    if ($jsonPayload === false || empty($jsonPayload)) {
        http_response_code(400);
        echo json_encode(['status' => 'error', 'message' => 'Nessun payload JSON ricevuto o payload vuoto.']);
        exit;
    }

    $data = json_decode($jsonPayload);

    if (json_last_error() !== JSON_ERROR_NONE) {
        http_response_code(400);
        echo json_encode(['status' => 'error', 'message' => 'Payload JSON non valido: ' . json_last_error_msg()]);
        exit;
    }

    $logMessage = "[" . date('Y-m-d H:i:s') . "] Ricevuto JSON: " . $jsonPayload . PHP_EOL;

    if (file_put_contents($logFile, $logMessage, FILE_APPEND | LOCK_EX) === false) {
        http_response_code(500);
        echo json_encode(['status' => 'error', 'message' => 'Impossibile scrivere nel file di log.']);
        error_log("Errore durante la scrittura nel file di log: " . $logFile);
    } else {
        http_response_code(200);
        echo json_encode(['status' => 'success', 'message' => 'Dati ricevuti e loggati.']);
    }

} else {
    http_response_code(405);
    echo json_encode(['status' => 'error', 'message' => 'Metodo non consentito. Si accettano solo richieste POST.']);
}
?>