using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Translator.Desktop;

internal static class UiLocalizer
{
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<string, string>> Originals = new();
    public static string Language { get; set; } = "tr";

    private static readonly Dictionary<string, string[]> Words = new(StringComparer.Ordinal)
    {
        ["Ayarlar"] = ["Settings", "Einstellungen", "Ajustes"],
        ["Geçmiş"] = ["History", "Verlauf", "Historial"],
        ["Kayıt geçmişi"] = ["Recording history", "Aufnahmeverlauf", "Historial de grabaciones"],
        ["Kayıt"] = ["Recording", "Aufnahme", "Grabación"],
        ["Kaynak"] = ["Source", "Quelle", "Fuente"],
        ["Mikrofon"] = ["Microphone", "Mikrofon", "Micrófono"],
        ["Seçilen uygulama"] = ["Selected application", "Ausgewählte Anwendung", "Aplicación seleccionada"],
        ["16 kHz WAV dosyası"] = ["16 kHz WAV file", "16-kHz-WAV-Datei", "Archivo WAV de 16 kHz"],
        ["Konuşma dili"] = ["Spoken language", "Gesprochene Sprache", "Idioma hablado"],
        ["Hedef dil"] = ["Target language", "Zielsprache", "Idioma de destino"],
        ["Kaydı başlat"] = ["Start recording", "Aufnahme starten", "Iniciar grabación"],
        ["Kaydı durdur"] = ["Stop recording", "Aufnahme stoppen", "Detener grabación"],
        ["⏸ Duraklat"] = ["⏸ Pause", "⏸ Pausieren", "⏸ Pausar"],
        ["▶ Devam et"] = ["▶ Resume", "▶ Fortsetzen", "▶ Reanudar"],
        ["Uygulama"] = ["Application", "Anwendung", "Aplicación"],
        ["Listeyi yenile"] = ["Refresh list", "Liste aktualisieren", "Actualizar lista"],
        ["Mikrofonu da dahil et"] = ["Include microphone", "Mikrofon einbeziehen", "Incluir micrófono"],
        ["Sesi WAV olarak sakla"] = ["Save audio as WAV", "Audio als WAV speichern", "Guardar audio como WAV"],
        ["Yüzen altyazı"] = ["Floating subtitles", "Schwebende Untertitel", "Subtítulos flotantes"],
        ["Oturumu dışa aktar"] = ["Export session", "Sitzung exportieren", "Exportar sesión"],
        ["Canlı metin"] = ["Live text", "Live-Text", "Texto en vivo"],
        ["Konuşma ve çeviri"] = ["Speech and translation", "Sprache und Übersetzung", "Voz y traducción"],
        ["Zamanı göster"] = ["Show timestamps", "Zeitstempel anzeigen", "Mostrar marcas de tiempo"],
        ["Oturum özeti"] = ["Session summary", "Sitzungszusammenfassung", "Resumen de sesión"],
        ["Konuşmanın özeti"] = ["Conversation summary", "Gesprächszusammenfassung", "Resumen de la conversación"],
        ["Tür"] = ["Type", "Typ", "Tipo"],
        ["Video / eğitim"] = ["Video / training", "Video / Schulung", "Vídeo / formación"],
        ["Video/eğitim"] = ["Video/training", "Video/Schulung", "Vídeo/formación"],
        ["Toplantı"] = ["Meeting", "Besprechung", "Reunión"],
        ["Uzunluk"] = ["Length", "Länge", "Longitud"],
        ["Kısa"] = ["Short", "Kurz", "Breve"],
        ["Ayrıntılı"] = ["Detailed", "Ausführlich", "Detallado"],
        ["Özet dili"] = ["Summary language", "Sprache der Zusammenfassung", "Idioma del resumen"],
        ["Özet oluştur"] = ["Create summary", "Zusammenfassung erstellen", "Crear resumen"],
        ["Henüz özet yok"] = ["No summary yet", "Noch keine Zusammenfassung", "Aún no hay resumen"],
        ["Özet isteği özgün konuşmayı seçili sağlayıcıya gönderir. Kaynak işaretleri yaklaşık konuşma zamanlarıdır."] =
            ["Summarization sends the original transcript to the selected provider. Source markers are approximate speech times.",
             "Die Zusammenfassung sendet das Originaltranskript an den gewählten Anbieter. Quellenmarkierungen sind ungefähre Sprechzeiten.",
             "El resumen envía la transcripción original al proveedor elegido. Las marcas de origen son tiempos aproximados."],
        ["Kayıtlar arasında ara"] = ["Search recordings", "Aufnahmen durchsuchen", "Buscar grabaciones"],
        ["Seçili kayıtta ara"] = ["Search selected recording", "Ausgewählte Aufnahme durchsuchen", "Buscar en la grabación"],
        ["Kayıt başlığı"] = ["Recording title", "Aufnahmetitel", "Título de grabación"],
        ["Başlığı kaydet"] = ["Save title", "Titel speichern", "Guardar título"],
        ["Özeti aç"] = ["Open summary", "Zusammenfassung öffnen", "Abrir resumen"],
        ["Kayıt özeti"] = ["Recording summary", "Aufnahmezusammenfassung", "Resumen de grabación"],
        ["Canlı çeviri"] = ["Live translation", "Live-Übersetzung", "Traducción en vivo"],
        ["Çalışma modu"] = ["Mode", "Modus", "Modo"],
        ["Çeviri · Transkripsiyon · Ses kaydı · Yapay zekâ özeti"] = ["Translation · Transcription · Audio recording · AI summaries", "Übersetzung · Transkription · Audioaufnahme · KI-Zusammenfassung", "Traducción · Transcripción · Grabación de audio · Resúmenes con IA"],
        ["Yalnızca transkripsiyon"] = ["Transcription only", "Nur Transkription", "Solo transcripción"],
        ["Transkripsiyon"] = ["Transcription", "Transkription", "Transcripción"],
        ["Konuşma metni"] = ["Speech transcript", "Gesprächstranskript", "Transcripción de voz"],
        ["Transkripsiyon bakiyesi"] = ["Transcription balance", "Transkriptionsguthaben", "Saldo de transcripción"],
        ["Transkripsiyon maliyeti"] = ["Transcription cost", "Transkriptionskosten", "Coste de transcripción"],
        ["Yöntem"] = ["Method", "Methode", "Método"],
        ["Canlı API anahtarı"] = ["Live API key", "Live-API-Schlüssel", "Clave API en vivo"],
        ["Anahtarı kaydet"] = ["Save key", "Schlüssel speichern", "Guardar clave"],
        ["Bağlantıyı dene"] = ["Test connection", "Verbindung testen", "Probar conexión"],
        ["Anahtar sayfası"] = ["API key page", "API-Schlüsselseite", "Página de claves"],
        ["Soniox API anahtarı"] = ["Soniox API key", "Soniox-API-Schlüssel", "Clave API de Soniox"],
        ["Soniox kaydet"] = ["Save Soniox", "Soniox speichern", "Guardar Soniox"],
        ["Soniox dene"] = ["Test Soniox", "Soniox testen", "Probar Soniox"],
        ["Soniox anahtar sayfası"] = ["Soniox key page", "Soniox-Schlüsselseite", "Página de claves Soniox"],
        ["Canlı DeepSeek API anahtarı"] = ["Live DeepSeek API key", "Live-DeepSeek-API-Schlüssel", "Clave API DeepSeek en vivo"],
        ["DeepSeek kaydet"] = ["Save DeepSeek", "DeepSeek speichern", "Guardar DeepSeek"],
        ["DeepSeek dene"] = ["Test DeepSeek", "DeepSeek testen", "Probar DeepSeek"],
        ["DeepSeek anahtar sayfası"] = ["DeepSeek key page", "DeepSeek-Schlüsselseite", "Página de claves DeepSeek"],
        ["1 · Soniox — konuşmayı yazıya döker"] = ["1 · Soniox — transcribes speech", "1 · Soniox — transkribiert Sprache", "1 · Soniox — transcribe la voz"],
        ["2 · DeepSeek — yazıya dökülen cümleleri çevirir"] = ["2 · DeepSeek — translates transcribed sentences", "2 · DeepSeek — übersetzt transkribierte Sätze", "2 · DeepSeek — traduce las frases transcritas"],
        ["Özetleyici"] = ["Summarizer", "Zusammenfassung", "Resumidor"],
        ["Sağlayıcı"] = ["Provider", "Anbieter", "Proveedor"],
        ["Model"] = ["Model", "Modell", "Modelo"],
        ["API anahtarı"] = ["API key", "API-Schlüssel", "Clave API"],
        ["Göster"] = ["Show", "Anzeigen", "Mostrar"],
        ["Yerleşim"] = ["Layout", "Layout", "Diseño"],
        ["İkisi"] = ["Both", "Beide", "Ambos"],
        ["Orijinal"] = ["Original", "Original", "Original"],
        ["Çeviri"] = ["Translation", "Übersetzung", "Traducción"],
        ["Alt alta"] = ["Stacked", "Untereinander", "Uno debajo del otro"],
        ["Yan yana"] = ["Side by side", "Nebeneinander", "Lado a lado"],
        ["Pencere genişliği"] = ["Window width", "Fensterbreite", "Ancho de ventana"],
        ["Yazı boyutu"] = ["Font size", "Schriftgröße", "Tamaño de texto"],
        ["Kayıt konumu"] = ["Save location", "Speicherort", "Ubicación de guardado"],
        ["Klasör seç"] = ["Choose folder", "Ordner wählen", "Elegir carpeta"],
        ["Ayarları kaydet"] = ["Save settings", "Einstellungen speichern", "Guardar ajustes"],
        ["Kaydet ve uygula"] = ["Save and apply", "Speichern und anwenden", "Guardar y aplicar"],
        ["Yüzen altyazı ayarları"] = ["Floating subtitle settings", "Einstellungen für schwebende Untertitel", "Ajustes de subtítulos flotantes"],
        ["Metin içinde ara"] = ["Search in text", "Im Text suchen", "Buscar en el texto"],
        ["Yeniden adlandır"] = ["Rename", "Umbenennen", "Renombrar"],
        ["Kopyala"] = ["Copy", "Kopieren", "Copiar"],
        ["Yapıştır"] = ["Paste", "Einfügen", "Pegar"],
        ["Sil"] = ["Delete", "Löschen", "Eliminar"],
        ["Kaydı"] = ["Recording", "Aufnahme", "Grabación"],
        ["Özeti"] = ["Summary", "Zusammenfassung", "Resumen"],
        ["Kayıt ve özeti"] = ["Recording and summary", "Aufnahme und Zusammenfassung", "Grabación y resumen"],
        ["Kopya"] = ["Copy", "Kopie", "Copia"],
        ["Kaydet"] = ["Save", "Speichern", "Guardar"],
        ["Dışa aktar"] = ["Export", "Exportieren", "Exportar"],
        ["RecapVox · Ayarlar"] = ["RecapVox · Settings", "RecapVox · Einstellungen", "RecapVox · Ajustes"],
        ["Canlı çeviri · Ses kaydı · Yapay zekâ özetleri"] = ["Live translation · Audio recording · AI summaries", "Live-Übersetzung · Audioaufnahme · KI-Zusammenfassungen", "Traducción en vivo · Grabación de audio · Resúmenes con IA"],
        ["Özet"] = ["Summary", "Zusammenfassung", "Resumen"],
        ["Bakiyeleri yenile"] = ["Refresh balances", "Guthaben aktualisieren", "Actualizar saldos"],
        ["Canlı bakiye"] = ["Live balance", "Live-Guthaben", "Saldo en vivo"],
        ["Özet bakiyesi"] = ["Summary balance", "Zusammenfassungs-Guthaben", "Saldo del resumen"],
        ["Sorgulanıyor…"] = ["Checking…", "Wird geprüft…", "Consultando…"],
        ["Anahtar yok"] = ["No API key", "Kein API-Schlüssel", "Sin clave API"],
        ["Bakiye alınamadı"] = ["Balance unavailable", "Guthaben nicht verfügbar", "Saldo no disponible"],
        ["API'den alınamıyor"] = ["Not exposed by API", "Nicht über API verfügbar", "No disponible por API"],
        ["Canlı çeviri maliyeti"] = ["Live translation cost", "Kosten der Live-Übersetzung", "Costo de traducción en vivo"],
        ["Özetleyici kullanımı"] = ["Summarizer usage", "Nutzung der Zusammenfassung", "Uso del resumidor"],
        ["Maliyeti yenile"] = ["Refresh cost", "Kosten aktualisieren", "Actualizar costo"],
        ["Maliyet sağlayıcıdan alınamıyor"] = ["Cost unavailable from provider", "Kosten beim Anbieter nicht verfügbar", "Costo no disponible del proveedor"],
        ["Maliyet bilinmiyor"] = ["Cost unknown", "Kosten unbekannt", "Costo desconocido"],
        ["Bilinmiyor"] = ["Unknown", "Unbekannt", "Desconocido"],
        ["Tahmini"] = ["Estimate", "Schätzung", "Estimación"],
        ["Özet yok"] = ["No summary", "Keine Zusammenfassung", "Sin resumen"],
        ["Soniox anahtarı yok"] = ["Soniox key missing", "Soniox-Schlüssel fehlt", "Falta la clave Soniox"],
        ["Soniox maliyeti sorgulanıyor"] = ["Checking Soniox cost", "Soniox-Kosten werden geprüft", "Consultando costo Soniox"],
        ["Soniox kesin tutar"] = ["Soniox actual charge", "Soniox-Istkosten", "Cargo real de Soniox"],
        ["Soniox kesin; DeepSeek maliyeti dahil değil"] = ["Soniox actual; DeepSeek cost excluded", "Soniox-Istkosten; DeepSeek nicht enthalten", "Soniox real; DeepSeek no incluido"],
        ["Soniox kullanım verisi alınamadı"] = ["Soniox usage unavailable", "Soniox-Nutzung nicht verfügbar", "Uso de Soniox no disponible"],
        ["Soniox kullanım kaydı henüz yok; sonra yenileyin"] = ["Soniox log not ready; refresh later", "Soniox-Protokoll noch nicht bereit; später aktualisieren", "Registro Soniox aún no disponible; actualice después"],
        ["Soniox kullanım kaydı henüz yok"] = ["Soniox log not ready", "Soniox-Protokoll noch nicht bereit", "Registro Soniox aún no disponible"],
        ["Sağlayıcı maliyeti API'den alınamıyor"] = ["Provider cost unavailable by API", "Anbieterkosten nicht über API verfügbar", "Costo del proveedor no disponible por API"],
        ["Seçili kaydı silmek istiyor musunuz?"] = ["Delete the selected recording?", "Ausgewählte Aufnahme löschen?", "¿Eliminar la grabación seleccionada?"],
        ["Özet isteği özgün konuşmayı seçili sağlayıcıya gönderir."] = ["The original transcript is sent to the selected summarizer.", "Das Originaltranskript wird an den gewählten Anbieter gesendet.", "La transcripción original se envía al resumidor seleccionado."],
        ["Değişiklikler kaydedilmedi. Kaydet ve uygula düğmesine basın."] = ["Changes are not saved. Select Save and apply.", "Änderungen sind nicht gespeichert. Speichern und anwenden wählen.", "Los cambios no están guardados. Seleccione Guardar y aplicar."],
        ["Kaydedilmemiş ayarlar"] = ["Unsaved settings", "Nicht gespeicherte Einstellungen", "Ajustes sin guardar"],
        ["Kaydedilmemiş ayarlar var. Kapatmadan önce kaydedip uygulamak ister misiniz?"] = ["There are unsaved settings. Save and apply before closing?", "Es gibt nicht gespeicherte Einstellungen. Vor dem Schließen speichern und anwenden?", "Hay ajustes sin guardar. ¿Guardarlos y aplicarlos antes de cerrar?"],
        ["Program dili"] = ["Interface language", "Programmsprache", "Idioma de la interfaz"],
        ["Türkçe"] = ["Turkish", "Türkisch", "Turco"],
        ["İngilizce"] = ["English", "Englisch", "Inglés"],
        ["Almanca"] = ["German", "Deutsch", "Alemán"],
        ["İspanyolca"] = ["Spanish", "Spanisch", "Español"],
        ["Kayıt seçin."] = ["Select a recording.", "Aufnahme auswählen.", "Seleccione una grabación."],
        ["Devam ediyor"] = ["Ongoing", "Läuft", "En curso"],
        ["blok"] = ["blocks", "Blöcke", "bloques"],
        ["Özet var"] = ["Has summary", "Zusammenfassung vorhanden", "Tiene resumen"],
        ["Hazır. Kaynağı seçip başlatabilirsiniz."] = ["Ready. Choose a source and start recording.", "Bereit. Quelle wählen und Aufnahme starten.", "Listo. Elija una fuente e inicie la grabación."],
        ["Kayıt duraklatıldı."] = ["Recording paused.", "Aufnahme pausiert.", "Grabación en pausa."],
        ["Kayıt devam ediyor."] = ["Recording resumed.", "Aufnahme fortgesetzt.", "Grabación reanudada."],
        ["Canlı bağlantı yenileniyor…"] = ["Reconnecting live audio…", "Live-Audio wird neu verbunden…", "Reconectando el audio en vivo…"],
        ["Canlı bağlantı art arda kesildi. Kaydı yeniden başlatın."] = ["The live connection dropped repeatedly. Start a new recording.", "Die Live-Verbindung wurde wiederholt getrennt. Starten Sie eine neue Aufnahme.", "La conexión en vivo se interrumpió varias veces. Inicie una nueva grabación."],
        ["Oturum bitti. Konuşma ve özetler Geçmiş bölümünde."] = ["Session ended. Speech and summaries are in History.", "Sitzung beendet. Sprache und Zusammenfassungen stehen im Verlauf.", "Sesión finalizada. La voz y los resúmenes están en Historial."],
        ["Bağlanıyor…"] = ["Connecting…", "Verbindung wird hergestellt…", "Conectando…"],
        ["Kayıt seçin"] = ["Select a recording", "Aufnahme auswählen", "Seleccione una grabación"]
        , ["bağlantısı kurulamadı:"] = ["connection failed:", "Verbindung fehlgeschlagen:", "conexión fallida:"]
        , ["Sunucu bağlantıyı kurulumdan önce kapattı."] = ["The server closed the connection before setup completed.", "Der Server hat die Verbindung vor Abschluss der Einrichtung geschlossen.", "El servidor cerró la conexión antes de completar la configuración."]
        , ["İstek reddedildi."] = ["Request rejected.", "Anfrage abgelehnt.", "Solicitud rechazada."]
        , ["API anahtarı gerekli."] = ["API key required.", "API-Schlüssel erforderlich.", "Se requiere una clave API."]
        , ["gerekli."] = ["is required.", "ist erforderlich.", "es obligatorio."]
        , ["[API anahtarı]"] = ["[API key]", "[API-Schlüssel]", "[clave API]"]
        , ["Tamam"] = ["OK", "OK", "Aceptar"]
        , ["Ayarlar kaydedildi."] = ["Settings saved.", "Einstellungen gespeichert.", "Ajustes guardados."]
        , ["Özet, ilgili konuşma kaydına eklendi."] = ["Summary added to the matching recording.", "Zusammenfassung zur passenden Aufnahme hinzugefügt.", "Resumen añadido a la grabación correspondiente."]
        , ["Önce bir konuşma oturumu oluşturun."] = ["Create a speech session first.", "Zuerst eine Sprachsitzung erstellen.", "Primero cree una sesión de voz."]
        , ["Hibrit canlı çeviri için DeepSeek anahtarını Ayarlar bölümüne girin."] = ["Enter the DeepSeek key for hybrid live translation in Settings.", "DeepSeek-Schlüssel für die hybride Live-Übersetzung in Einstellungen eingeben.", "Introduzca la clave DeepSeek para la traducción híbrida en Ajustes."]
        , ["Önce sesini dinlemek istediğiniz uygulamayı seçin."] = ["Choose the application to listen to first.", "Zuerst die abzuhörende Anwendung wählen.", "Primero elija la aplicación que desea escuchar."]
        , ["Seçilen uygulama kapandı. Listeyi yenileyin."] = ["The selected application closed. Refresh the list.", "Die ausgewählte Anwendung wurde geschlossen. Liste aktualisieren.", "La aplicación seleccionada se cerró. Actualice la lista."]
        , ["Uygulama sesi durdu."] = ["Application audio stopped.", "Anwendungsaudio wurde gestoppt.", "Se detuvo el audio de la aplicación."]
        , ["WAV dosyası gerçek zaman hızında gönderiliyor."] = ["Sending the WAV file at real-time speed.", "WAV-Datei wird in Echtzeit gesendet.", "Enviando el archivo WAV a velocidad real."]
        , ["Seçilen uygulama ve mikrofon birlikte dinleniyor; konuşma dili otomatik algılanıyor."] = ["Application and microphone are being captured together; spoken language is detected automatically.", "Anwendung und Mikrofon werden gemeinsam erfasst; die Sprache wird automatisch erkannt.", "Se capturan juntos la aplicación y el micrófono; el idioma se detecta automáticamente."]
        , ["Seçilen uygulamanın sesi dinleniyor. Ses üretmiyorsa metin görünmez."] = ["Listening to the selected application. No text appears without audio.", "Audio der ausgewählten Anwendung wird erfasst. Ohne Ton erscheint kein Text.", "Escuchando la aplicación seleccionada. Sin audio no aparecerá texto."]
        , ["Dinlenen kaynak: Uygulama seçilmedi."] = ["Audio source: no application selected.", "Audioquelle: keine Anwendung ausgewählt.", "Fuente de audio: no se seleccionó ninguna aplicación."]
        , ["Dinlenecek kaynak: Seçilecek WAV dosyası"] = ["Audio source: WAV file to choose", "Audioquelle: WAV-Datei auswählen", "Fuente de audio: archivo WAV por elegir"]
        , ["Dinlenecek kaynak: Mikrofon"] = ["Audio source: microphone", "Audioquelle: Mikrofon", "Fuente de audio: micrófono"]
        , ["Dışa aktarılacak konuşma veya özet yok."] = ["There is no speech or summary to export.", "Keine Sprache oder Zusammenfassung zum Exportieren vorhanden.", "No hay voz ni resumen para exportar."]
        , ["Dışa aktarma biçimini ve konumunu seçin"] = ["Choose export format and location", "Exportformat und Speicherort wählen", "Elija formato y ubicación de exportación"]
        , ["Kayıt klasörünü seçin"] = ["Choose recording folder", "Aufnahmeordner wählen", "Elija la carpeta de grabación"]
        , ["Soniox anahtarı kaydedildi."] = ["Soniox key saved.", "Soniox-Schlüssel gespeichert.", "Clave Soniox guardada."]
        , ["Canlı DeepSeek anahtarı kaydedildi."] = ["Live DeepSeek key saved.", "Live-DeepSeek-Schlüssel gespeichert.", "Clave DeepSeek en vivo guardada."]
        , ["Soniox bağlantısı başarılı. Ses olmadan konuşma tanıma kalitesi ölçülmez."] = ["Soniox connection successful. Speech recognition quality needs an audio test.", "Soniox-Verbindung erfolgreich. Die Spracherkennungsqualität erfordert einen Audiotest.", "Conexión Soniox correcta. La calidad de reconocimiento requiere una prueba de audio."]
        , ["DeepSeek çeviri denemesi başarılı. Deneme sağlayıcıda kullanıma yazılabilir."] = ["DeepSeek translation test succeeded. The test may count toward provider usage.", "DeepSeek-Übersetzungstest erfolgreich. Der Test kann beim Anbieter als Nutzung zählen.", "Prueba de traducción DeepSeek correcta. Puede contar como uso del proveedor."]
        , ["Başlık 120 karakteri geçemez."] = ["Title must be 120 characters or fewer.", "Der Titel darf höchstens 120 Zeichen lang sein.", "El título debe tener 120 caracteres o menos."]
        , ["Yazarak dil adı veya kodu arayın"] = ["Type to search by language name or code", "Sprache nach Name oder Code suchen", "Escriba para buscar por nombre o código"]
        , ["Bu sağlayıcı konuşma dilini otomatik algılar; elle kaynak dili seçme ayarı yok."] = ["This provider detects spoken language automatically; manual source selection is unavailable.", "Dieser Anbieter erkennt die Sprache automatisch; eine manuelle Quellsprachwahl ist nicht verfügbar.", "Este proveedor detecta el idioma automáticamente; no permite elegirlo manualmente."]
        , ["Yazarak dil adı veya kodu arayın; otomatik algılama da seçilebilir."] = ["Type to search by language name or code; automatic detection is also available.", "Sprache nach Name oder Code suchen; automatische Erkennung ist ebenfalls verfügbar.", "Escriba para buscar por nombre o código; también puede detectar automáticamente."]
        , ["Ses, Ayarlar bölümündeki kayıt klasörüne kaydedilir"] = ["Audio is saved in the recording folder set in Settings", "Audio wird im unter Einstellungen festgelegten Aufnahmeordner gespeichert", "El audio se guarda en la carpeta indicada en Ajustes"]
        , ["Otomatik algıla"] = ["Auto-detect", "Automatisch erkennen", "Detectar automáticamente"]
        , ["ile özet hazırlanıyor…"] = ["is preparing the summary…", "erstellt die Zusammenfassung…", "está preparando el resumen…"]
        , ["Mikrofon dinleniyor;"] = ["Listening to microphone;", "Mikrofon wird erfasst;", "Escuchando el micrófono;"]
        , ["bağlantısı açık."] = ["connection is open.", "Verbindung ist offen.", "conexión abierta."]
        , ["Uygulama sesi hatası:"] = ["Application audio error:", "Anwendungsaudiofehler:", "Error de audio de la aplicación:"]
        , ["anahtar kaydedildi."] = ["key saved.", "Schlüssel gespeichert.", "clave guardada."]
        , ["anahtar ve model kaydedildi."] = ["key and model saved.", "Schlüssel und Modell gespeichert.", "clave y modelo guardados."]
        , ["bağlantı başarılı; deneme kullanıma yazılabilir."] = ["connection succeeded; the test may count toward usage.", "Verbindung erfolgreich; der Test kann als Nutzung zählen.", "conexión correcta; la prueba puede contar como uso."]
        , ["Soniox konuşma ve çeviriyi tek bağlantıda üretir."] = ["Soniox transcribes and translates through one connection.", "Soniox transkribiert und übersetzt über eine Verbindung.", "Soniox transcribe y traduce con una sola conexión."]
        , ["Her anahtarı kendi bölümünden kaydedip deneyin. Canlı DeepSeek anahtarı özetleyiciden bağımsızdır."] =
            ["Save and test each key in its section. The live DeepSeek key is independent of the summarizer.",
             "Jeden Schlüssel im eigenen Abschnitt speichern und testen. Der Live-DeepSeek-Schlüssel ist unabhängig von der Zusammenfassung.",
             "Guarde y pruebe cada clave en su sección. La clave DeepSeek en vivo es independiente del resumidor."]
        , ["OpenAI gpt-realtime-translate: ses ve iki dilde metin üretir. 24 kHz ses akışı kullanılır."] =
            ["OpenAI gpt-realtime-translate produces audio and text in two languages; it uses 24 kHz audio.",
             "OpenAI gpt-realtime-translate erzeugt Audio und Text in zwei Sprachen; verwendet 24-kHz-Audio.",
             "OpenAI gpt-realtime-translate produce audio y texto en dos idiomas; usa audio de 24 kHz."]
        , ["Gemini 3.5 Live Translate: ses ve iki dilde metin üretir. 16 kHz ses akışı kullanılır."] =
            ["Gemini 3.5 Live Translate produces audio and text in two languages; it uses 16 kHz audio.",
             "Gemini 3.5 Live Translate erzeugt Audio und Text in zwei Sprachen; verwendet 16-kHz-Audio.",
             "Gemini 3.5 Live Translate produce audio y texto en dos idiomas; usa audio de 16 kHz."]
        , ["Dinlenen kaynak:"] = ["Audio source:", "Audioquelle:", "Fuente de audio:"]
        , ["Dinlenen kaynak: Mikrofon"] = ["Audio source: microphone", "Audioquelle: Mikrofon", "Fuente de audio: micrófono"]
        , ["Dinlenecek uygulama:"] = ["Application to capture:", "Zu erfassende Anwendung:", "Aplicación para capturar:"]
        , ["mikrofon"] = ["microphone", "Mikrofon", "micrófono"]
        , ["mikrofon; konuşma dili otomatik algılanır;"] = ["microphone; spoken language is detected automatically;", "Mikrofon; Sprache wird automatisch erkannt;", "micrófono; el idioma se detecta automáticamente;"]
        , ["Bu uygulamanın tüm pencereleri ve alt süreçleri; tek sekme seçimi yok."] =
            ["All windows and child processes of this application; individual tabs cannot be selected.",
             "Alle Fenster und Unterprozesse dieser Anwendung; einzelne Tabs können nicht gewählt werden.",
             "Todas las ventanas y procesos secundarios; no se pueden elegir pestañas individuales."]
        , ["tüm pencereleri ve alt süreçleri. Pencere başlığı sadece seçim içindir."] =
            ["all windows and child processes. The window title is only for selection.",
             "alle Fenster und Unterprozesse. Der Fenstertitel dient nur zur Auswahl.",
             "todas las ventanas y procesos secundarios. El título solo sirve para seleccionar."]
        , ["PDF belgesi"] = ["PDF document", "PDF-Dokument", "Documento PDF"]
        , ["Düz metin"] = ["Plain text", "Klartext", "Texto plano"]
        , ["Oturum"] = ["Session", "Sitzung", "Sesión"]
        , ["olarak dışa aktarıldı."] = ["exported.", "exportiert.", "exportada."]
    };

    public static string T(string original)
    {
        if (Language == "tr" || !Words.TryGetValue(original, out var variants)) return original;
        return variants[Language switch { "en" => 0, "de" => 1, "es" => 2, _ => 0 }];
    }

    public static string TranslateCurrent(string current)
    {
        foreach (var word in Words)
            if (current == word.Key || word.Value.Contains(current, StringComparer.Ordinal)) return T(word.Key);
        return current;
    }

    public static void Apply(DependencyObject root)
    {
        TranslateElement(root);
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) Apply(child);
    }

    private static void TranslateElement(DependencyObject element)
    {
        var originals = Originals.GetOrCreateValue(element);
        if (element is Window window) window.Title = TranslateStored(originals, "Title", window.Title);
        if (element is TextBlock block && block.Name is not ("SourceInfoText" or "StatusText" or "LiveProviderText" or "SummaryProviderText" or "SummaryDateText" or "FeedbackText" or "LiveHintText" or "RecordTimerText"))
            block.Text = TranslateStored(originals, "Text", block.Text);
        if (element is ContentControl control && control.Content is string value)
            control.Content = TranslateStored(originals, "Content", value);
        if (element is HeaderedContentControl header && header.Header is string title)
            header.Header = TranslateStored(originals, "Header", title);
        if (element is FrameworkElement framework && framework.ToolTip is string hint)
            framework.ToolTip = TranslateStored(originals, "ToolTip", hint);
    }

    private static string TranslateStored(Dictionary<string, string> originals, string property, string current)
    {
        if (!originals.TryGetValue(property, out var original)) originals[property] = original = current;
        return T(original);
    }
}
