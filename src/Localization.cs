using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace EasyIpSwitch
{
    internal enum AppLanguage
    {
        French = 0,
        English = 1,
        German = 2,
        Spanish = 3
    }

    /// <summary>
    /// Traductions de l'interface. Les textes sources sont en français ; une
    /// clé absente d'une langue retombe automatiquement sur le français.
    /// </summary>
    internal static class L
    {
        private const string RegistryPath = @"Software\Angeli Visions\Easy IP-Switch";
        private const string RegistryValue = "Language";

        private static AppLanguage _current = AppLanguage.French;
        private static Dictionary<string, string> _english;
        private static Dictionary<string, string> _german;
        private static Dictionary<string, string> _spanish;
        private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
        private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

        public static event EventHandler LanguageChanged;

        static L()
        {
            _english = new Dictionary<string, string>(StringComparer.Ordinal);
            _german = new Dictionary<string, string>(StringComparer.Ordinal);
            _spanish = new Dictionary<string, string>(StringComparer.Ordinal);
            BuildTranslations();
        }

        public static AppLanguage Current
        {
            get { return _current; }
        }

        public static CultureInfo Culture
        {
            get
            {
                switch (_current)
                {
                    case AppLanguage.English: return EnglishCulture;
                    case AppLanguage.German: return GermanCulture;
                    case AppLanguage.Spanish: return SpanishCulture;
                    default: return FrenchCulture;
                }
            }
        }

        public static string DisplayName(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return "English";
                case AppLanguage.German: return "Deutsch";
                case AppLanguage.Spanish: return "Español";
                default: return "Français";
            }
        }

        public static string Code(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return "en";
                case AppLanguage.German: return "de";
                case AppLanguage.Spanish: return "es";
                default: return "fr";
            }
        }

        public static AppLanguage Parse(string code)
        {
            if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)) return AppLanguage.English;
            if (string.Equals(code, "de", StringComparison.OrdinalIgnoreCase)) return AppLanguage.German;
            if (string.Equals(code, "es", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Spanish;
            return AppLanguage.French;
        }

        public static void Initialize()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(RegistryValue);
                        string code = value as string;
                        if (!string.IsNullOrEmpty(code))
                        {
                            _current = Parse(code);
                        }
                    }
                }
            }
            catch
            {
                _current = AppLanguage.French;
            }
        }

        public static void SetLanguage(AppLanguage language)
        {
            SetLanguage(language, true);
        }

        internal static void SetLanguage(AppLanguage language, bool persist)
        {
            if (_current == language)
            {
                return;
            }
            _current = language;
            if (persist)
            {
                SaveLanguage();
            }
            if (LanguageChanged != null)
            {
                LanguageChanged(null, EventArgs.Empty);
            }
        }

        private static void SaveLanguage()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue(RegistryValue, Code(_current), RegistryValueKind.String);
                    }
                }
            }
            catch
            {
            }
        }

        public static string T(string french)
        {
            if (string.IsNullOrEmpty(french) || _current == AppLanguage.French)
            {
                return french;
            }
            Dictionary<string, string> map = Map(_current);
            string translated;
            if (map != null && map.TryGetValue(french, out translated))
            {
                return translated;
            }
            return french;
        }

        public static string F(string french, params object[] args)
        {
            return string.Format(Culture, T(french), args);
        }

        private static Dictionary<string, string> Map(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return _english;
                case AppLanguage.German: return _german;
                case AppLanguage.Spanish: return _spanish;
                default: return null;
            }
        }

        private static void Add(string french, string english, string german, string spanish)
        {
            _english[french] = english;
            _german[french] = german;
            _spanish[french] = spanish;
        }

        private static void BuildTranslations()
        {
            // Commun
            Add("Actualiser", "Refresh", "Aktualisieren", "Actualizar");
            Add("Cartes réseau", "Network cards", "Netzwerkkarten", "Tarjetas de red");
            Add("Test de ping", "Ping test", "Ping-Test", "Prueba de ping");
            Add("Connexions réseau", "Network connections", "Netzwerkverbindungen", "Conexiones de red");
            Add("Faire un don", "Donate", "Spenden", "Hacer una donación");
            Add("Changer", "Apply", "Übernehmen", "Aplicar");
            Add("Annuler", "Cancel", "Abbrechen", "Cancelar");
            Add("Tester", "Test", "Testen", "Probar");
            Add("Rester", "Stay", "Bleiben", "Quedarse");
            Add("Quitter", "Quit", "Beenden", "Salir");
            Add("Quitter Easy IP-Switch ?", "Quit Easy IP-Switch?", "Easy IP-Switch beenden?", "¿Salir de Easy IP-Switch?");
            Add("Appliquer et quitter", "Apply and quit", "Übernehmen und beenden", "Aplicar y salir");
            Add("Quitter sans appliquer", "Quit without applying", "Beenden ohne Übernehmen", "Salir sin aplicar");
            Add("Langue", "Language", "Sprache", "Idioma");
            Add("Changer la langue de l'application", "Change the application language", "Sprache der Anwendung ändern", "Cambiar el idioma de la aplicación");
            Add("Administrateur", "Administrator", "Administrator", "Administrador");
            Add("Droits limités", "Limited rights", "Eingeschränkte Rechte", "Permisos limitados");
            Add("Mode clair ou sombre", "Light or dark mode", "Heller oder dunkler Modus", "Modo claro u oscuro");
            Add("Soutenir Easy IP-Switch", "Support Easy IP-Switch", "Easy IP-Switch unterstützen", "Apoyar Easy IP-Switch");
            Add("Scannez ce QR code ou cliquez dessus pour faire un don via PayPal.",
                "Scan this QR code or click it to donate via PayPal.",
                "Scannen Sie diesen QR-Code oder klicken Sie darauf, um über PayPal zu spenden.",
                "Escanee este código QR o haga clic en él para donar mediante PayPal.");
            Add("Ouvrez la page de don ci-dessous pour soutenir le projet.",
                "Open the donation page below to support the project.",
                "Öffnen Sie unten die Spendenseite, um das Projekt zu unterstützen.",
                "Abra la página de donación de abajo para apoyar el proyecto.");
            Add("Ouvrir la page de don", "Open the donation page", "Spendenseite öffnen", "Abrir la página de donación");
            Add("QR code pour faire un don", "QR code for donations", "QR-Code für Spenden", "Código QR para donaciones");
            Add("Ouvre la page de don PayPal.", "Opens the PayPal donation page.", "Öffnet die PayPal-Spendenseite.", "Abre la página de donación de PayPal.");
            Add("Impossible d'ouvrir la page de don :\r\n\r\n{0}",
                "Unable to open the donation page:\r\n\r\n{0}",
                "Die Spendenseite konnte nicht geöffnet werden:\r\n\r\n{0}",
                "No se pudo abrir la página de donación:\r\n\r\n{0}");

            // En-tête, onglets et pied de page
            Add("Changement d'adresse IP en quelques clics",
                "Change an IP address in a few clicks",
                "IP-Adresse in wenigen Klicks ändern",
                "Cambiar una dirección IP en unos clics");
            Add("Mettre à jour la liste et l'état des cartes réseau (F5)",
                "Update the network card list and status (F5)",
                "Liste und Status der Netzwerkkarten aktualisieren (F5)",
                "Actualizar la lista y el estado de las tarjetas de red (F5)");
            Add("Cliquez pour relancer Easy IP-Switch avec les droits administrateur",
                "Click to restart Easy IP-Switch as administrator",
                "Klicken Sie, um Easy IP-Switch als Administrator neu zu starten",
                "Haga clic para reiniciar Easy IP-Switch como administrador");
            Add("Easy IP-Switch dispose des droits administrateur.",
                "Easy IP-Switch has administrator rights.",
                "Easy IP-Switch verfügt über Administratorrechte.",
                "Easy IP-Switch tiene permisos de administrador.");
            Add("Cliquez ici pour relancer Easy IP-Switch avec les droits administrateur.",
                "Click here to restart Easy IP-Switch as administrator.",
                "Klicken Sie hier, um Easy IP-Switch als Administrator neu zu starten.",
                "Haga clic aquí para reiniciar Easy IP-Switch como administrador.");
            Add("Ouvre les Connexions réseau Windows et crée un raccourci sur le Bureau.",
                "Opens Windows Network Connections and creates a desktop shortcut.",
                "Öffnet die Windows-Netzwerkverbindungen und erstellt eine Desktop-Verknüpfung.",
                "Abre las Conexiones de red de Windows y crea un acceso directo en el escritorio.");
            Add("Ouvre les Connexions réseau Windows et crée leur raccourci sur le Bureau.",
                "Opens Windows Network Connections and creates their desktop shortcut.",
                "Öffnet die Windows-Netzwerkverbindungen und erstellt deren Desktop-Verknüpfung.",
                "Abre las Conexiones de red de Windows y crea su acceso directo en el escritorio.");
            Add("Recherche des cartes réseau…", "Searching for network cards…", "Netzwerkkarten werden gesucht…", "Buscando tarjetas de red…");
            Add("Actualisation des cartes réseau…", "Refreshing network cards…", "Netzwerkkarten werden aktualisiert…", "Actualizando tarjetas de red…");
            Add("Recherche des cartes réseau.", "Searching for network cards.", "Netzwerkkarten werden gesucht.", "Buscando tarjetas de red.");
            Add("© {0} Angeli Visions. Tous droits réservés.",
                "© {0} Angeli Visions. All rights reserved.",
                "© {0} Angeli Visions. Alle Rechte vorbehalten.",
                "© {0} Angeli Visions. Todos los derechos reservados.");

            // Messages principaux
            Add("Opération en cours : attendez la fin du traitement avant de fermer.",
                "Operation in progress: wait until it finishes before closing.",
                "Vorgang läuft: Warten Sie, bis er abgeschlossen ist, bevor Sie schließen.",
                "Operación en curso: espere a que termine antes de cerrar.");
            Add("Aucune modification n'est en attente.",
                "No changes are pending.",
                "Keine Änderungen ausstehend.",
                "No hay cambios pendientes.");
            Add("Merci d'utiliser Easy IP-Switch. Avant de fermer, vous pouvez soutenir le projet : scannez le QR code ou ouvrez la page de don.",
                "Thank you for using Easy IP-Switch. Before closing, you can support the project: scan the QR code or open the donation page.",
                "Danke, dass Sie Easy IP-Switch verwenden. Vor dem Schließen können Sie das Projekt unterstützen: Scannen Sie den QR-Code oder öffnen Sie die Spendenseite.",
                "Gracias por usar Easy IP-Switch. Antes de cerrar, puede apoyar el proyecto: escanee el código QR o abra la página de donación.");
            Add("Une carte réseau a été modifiée mais la nouvelle configuration n'a pas encore été appliquée.",
                "A network card was changed but the new configuration has not been applied yet.",
                "Eine Netzwerkkarte wurde geändert, aber die neue Konfiguration wurde noch nicht übernommen.",
                "Se modificó una tarjeta de red, pero la nueva configuración aún no se ha aplicado.");
            Add("{0} cartes réseau ont été modifiées mais leurs nouvelles configurations n'ont pas encore été appliquées.",
                "{0} network cards were changed but their new configurations have not been applied yet.",
                "{0} Netzwerkkarten wurden geändert, aber ihre neuen Konfigurationen wurden noch nicht übernommen.",
                "Se modificaron {0} tarjetas de red, pero sus nuevas configuraciones aún no se han aplicado.");
            Add("Vous pouvez appliquer ces changements avant de fermer, quitter sans les appliquer (les saisies seront perdues) ou rester dans l'application.",
                "You can apply these changes before closing, quit without applying them (your entries will be lost), or stay in the application.",
                "Sie können diese Änderungen vor dem Schließen übernehmen, ohne Übernehmen beenden (Ihre Eingaben gehen verloren) oder in der Anwendung bleiben.",
                "Puede aplicar estos cambios antes de cerrar, salir sin aplicarlos (se perderán los datos introducidos) o permanecer en la aplicación.");
            Add("Fermeture annulée.", "Close cancelled.", "Schließen abgebrochen.", "Cierre cancelado.");
            Add("Fermeture annulée : les modifications en attente sont conservées.",
                "Close cancelled: pending changes are kept.",
                "Schließen abgebrochen: Ausstehende Änderungen bleiben erhalten.",
                "Cierre cancelado: se conservan los cambios pendientes.");
            Add("Activez la carte avant d'appliquer une configuration.",
                "Enable the card before applying a configuration.",
                "Aktivieren Sie die Karte, bevor Sie eine Konfiguration übernehmen.",
                "Active la tarjeta antes de aplicar una configuración.");
            Add("Fermeture annulée : activez d'abord « {0} ».",
                "Close cancelled: enable “{0}” first.",
                "Schließen abgebrochen: Aktivieren Sie zuerst „{0}“.",
                "Cierre cancelado: active primero «{0}».");
            Add("Fermeture annulée : corrigez les champs signalés en rouge dans « {0} ».",
                "Close cancelled: fix the fields marked in red in “{0}”.",
                "Schließen abgebrochen: Korrigieren Sie die rot markierten Felder in „{0}“.",
                "Cierre cancelado: corrija los campos marcados en rojo en «{0}».");
            Add("Droits administrateur requis pour appliquer les modifications : fermeture annulée.",
                "Administrator rights are required to apply the changes: close cancelled.",
                "Zum Übernehmen der Änderungen sind Administratorrechte erforderlich: Schließen abgebrochen.",
                "Se requieren permisos de administrador para aplicar los cambios: cierre cancelado.");
            Add("Application des modifications avant fermeture…",
                "Applying changes before closing…",
                "Änderungen werden vor dem Schließen übernommen…",
                "Aplicando cambios antes de cerrar…");
            Add("Fermeture annulée : {0}", "Close cancelled: {0}", "Schließen abgebrochen: {0}", "Cierre cancelado: {0}");

            // Barre d'état et actualisation
            Add("Une opération est en cours ; réessayez dans un instant.",
                "An operation is in progress; try again in a moment.",
                "Ein Vorgang läuft; versuchen Sie es gleich noch einmal.",
                "Hay una operación en curso; inténtelo de nuevo en un momento.");
            Add("Une actualisation est en cours ; une nouvelle lecture suivra.",
                "A refresh is in progress; another read will follow.",
                "Eine Aktualisierung läuft; eine weitere Abfrage folgt.",
                "Hay una actualización en curso; se realizará otra lectura.");
            Add("Abandonner les modifications ?", "Discard changes?", "Änderungen verwerfen?", "¿Descartar los cambios?");
            Add("Des saisies n'ont pas encore été appliquées. L'actualisation des cartes réseau effacera ces modifications.",
                "Some entries have not been applied yet. Refreshing the network cards will erase these changes.",
                "Einige Eingaben wurden noch nicht übernommen. Durch die Aktualisierung der Netzwerkkarten werden diese Änderungen gelöscht.",
                "Algunos datos aún no se han aplicado. Actualizar las tarjetas de red borrará estos cambios.");
            Add("Actualiser et abandonner", "Refresh and discard", "Aktualisieren und verwerfen", "Actualizar y descartar");
            Add("Actualiser sans perdre les saisies", "Refresh without losing entries", "Aktualisieren ohne Eingaben zu verlieren", "Actualizar sin perder los datos");
            Add("Erreur inconnue.", "Unknown error.", "Unbekannter Fehler.", "Error desconocido.");
            Add("Impossible de lire les cartes réseau.\n\n{0}",
                "Unable to read the network cards.\n\n{0}",
                "Die Netzwerkkarten konnten nicht gelesen werden.\n\n{0}",
                "No se pudieron leer las tarjetas de red.\n\n{0}");
            Add("Impossible de lire les cartes réseau : {0}",
                "Unable to read the network cards: {0}",
                "Die Netzwerkkarten konnten nicht gelesen werden: {0}",
                "No se pudieron leer las tarjetas de red: {0}");
            Add("Actualisation terminée ; l'état d'une carte est encore en cours de vérification.",
                "Refresh complete; a card status is still being verified.",
                "Aktualisierung abgeschlossen; der Status einer Karte wird noch geprüft.",
                "Actualización completada; aún se está verificando el estado de una tarjeta.");
            Add("Actualisation terminée : 1 carte réseau détectée.",
                "Refresh complete: 1 network card detected.",
                "Aktualisierung abgeschlossen: 1 Netzwerkkarte erkannt.",
                "Actualización completada: 1 tarjeta de red detectada.");
            Add("Actualisation terminée : {0} cartes réseau détectées.",
                "Refresh complete: {0} network cards detected.",
                "Aktualisierung abgeschlossen: {0} Netzwerkkarten erkannt.",
                "Actualización completada: {0} tarjetas de red detectadas.");
            Add("Aucune carte réseau détectée.\n\nBranchez un câble réseau, activez une carte, puis cliquez sur Actualiser.",
                "No network card detected.\n\nConnect a network cable, enable a card, then click Refresh.",
                "Keine Netzwerkkarte erkannt.\n\nSchließen Sie ein Netzwerkkabel an, aktivieren Sie eine Karte und klicken Sie dann auf Aktualisieren.",
                "No se detectó ninguna tarjeta de red.\n\nConecte un cable de red, active una tarjeta y haga clic en Actualizar.");
            Add("Carte activée.", "Card enabled.", "Karte aktiviert.", "Tarjeta activada.");
            Add("Carte désactivée.", "Card disabled.", "Karte deaktiviert.", "Tarjeta desactivada.");
            Add("Opération en cours, veuillez patienter.", "Operation in progress, please wait.", "Vorgang läuft, bitte warten.", "Operación en curso, espere por favor.");
            Add("Opération en cours, veuillez patienter…", "Operation in progress, please wait…", "Vorgang läuft, bitte warten…", "Operación en curso, espere por favor…");
            Add("1 carte modifiée — utilisez le bouton Changer de la carte concernée.",
                "1 card changed — use the Apply button on that card.",
                "1 Karte geändert — verwenden Sie die Schaltfläche Übernehmen der betreffenden Karte.",
                "1 tarjeta modificada: use el botón Aplicar de esa tarjeta.");
            Add("{0} cartes modifiées — utilisez le bouton Changer de chaque carte.",
                "{0} cards changed — use the Apply button on each card.",
                "{0} Karten geändert — verwenden Sie die Schaltfläche Übernehmen jeder Karte.",
                "{0} tarjetas modificadas: use el botón Aplicar de cada tarjeta.");
            Add("Chargement des cartes réseau…", "Loading network cards…", "Netzwerkkarten werden geladen…", "Cargando tarjetas de red…");
            Add("Aucune modification en attente.", "No pending changes.", "Keine ausstehenden Änderungen.", "No hay cambios pendientes.");
            Add("1 carte modifiée - utilisez le bouton Changer de la carte concernée.",
                "1 card changed - use the Apply button on that card.",
                "1 Karte geändert - verwenden Sie die Schaltfläche Übernehmen der betreffenden Karte.",
                "1 tarjeta modificada: use el botón Aplicar de esa tarjeta.");
            Add("{0} cartes modifiées - utilisez le bouton Changer de chaque carte.",
                "{0} cards changed - use the Apply button on each card.",
                "{0} Karten geändert - verwenden Sie die Schaltfläche Übernehmen jeder Karte.",
                "{0} tarjetas modificadas: use el botón Aplicar de cada tarjeta.");
            Add("Changement refusé : activez d'abord la carte.",
                "Change refused: enable the card first.",
                "Änderung abgelehnt: Aktivieren Sie zuerst die Karte.",
                "Cambio rechazado: active primero la tarjeta.");
            Add("Changement refusé pour « {0} » : corrigez les champs signalés en rouge.",
                "Change refused for “{0}”: fix the fields marked in red.",
                "Änderung für „{0}“ abgelehnt: Korrigieren Sie die rot markierten Felder.",
                "Cambio rechazado para «{0}»: corrija los campos marcados en rojo.");
            Add("Droits administrateur requis pour appliquer la configuration.",
                "Administrator rights are required to apply the configuration.",
                "Zum Übernehmen der Konfiguration sind Administratorrechte erforderlich.",
                "Se requieren permisos de administrador para aplicar la configuración.");
            Add("Application de la configuration de « {0} »…",
                "Applying the configuration of “{0}”…",
                "Konfiguration von „{0}“ wird übernommen…",
                "Aplicando la configuración de «{0}»…");
            Add("Une erreur inattendue est survenue : {0}",
                "An unexpected error occurred: {0}",
                "Ein unerwarteter Fehler ist aufgetreten: {0}",
                "Se produjo un error inesperado: {0}");
            Add("Modification appliquée avec succès.", "Change applied successfully.", "Änderung erfolgreich übernommen.", "Cambio aplicado correctamente.");
            Add("Modifications appliquées avec succès.", "Changes applied successfully.", "Änderungen erfolgreich übernommen.", "Cambios aplicados correctamente.");
            Add("Certaines adresses sont enregistrées, mais leur configuration reste à vérifier.",
                "Some addresses are saved, but their configuration still needs verification.",
                "Einige Adressen sind gespeichert, ihre Konfiguration muss jedoch noch geprüft werden.",
                "Algunas direcciones están guardadas, pero su configuración aún debe verificarse.");
            Add("Certaines configurations ne sont pas encore confirmées. Actualisez avant de réessayer.",
                "Some configurations are not confirmed yet. Refresh before trying again.",
                "Einige Konfigurationen sind noch nicht bestätigt. Aktualisieren Sie, bevor Sie es erneut versuchen.",
                "Algunas configuraciones aún no están confirmadas. Actualice antes de volver a intentarlo.");
            Add("Les modifications ont échoué. Consultez le détail au bas des cartes.",
                "The changes failed. See the details at the bottom of the cards.",
                "Die Änderungen sind fehlgeschlagen. Details finden Sie unten in den Karten.",
                "Los cambios fallaron. Consulte los detalles al pie de las tarjetas.");
            Add("{0} modification(s) sur {1} appliquée(s). Consultez le détail au bas des cartes.",
                "{0} of {1} change(s) applied. See the details at the bottom of the cards.",
                "{0} von {1} Änderung(en) übernommen. Details finden Sie unten in den Karten.",
                "{0} de {1} cambio(s) aplicado(s). Consulte los detalles al pie de las tarjetas.");
            Add("Fermeture annulée : vérifiez les configurations non confirmées.",
                "Close cancelled: check the unconfirmed configurations.",
                "Schließen abgebrochen: Prüfen Sie die unbestätigten Konfigurationen.",
                "Cierre cancelado: compruebe las configuraciones no confirmadas.");
            Add("Fermeture annulée : certaines modifications n'ont pas pu être appliquées.",
                "Close cancelled: some changes could not be applied.",
                "Schließen abgebrochen: Einige Änderungen konnten nicht übernommen werden.",
                "Cierre cancelado: no se pudieron aplicar algunos cambios.");
            Add("Droits administrateur requis pour activer ou désactiver une carte.",
                "Administrator rights are required to enable or disable a card.",
                "Zum Aktivieren oder Deaktivieren einer Karte sind Administratorrechte erforderlich.",
                "Se requieren permisos de administrador para activar o desactivar una tarjeta.");
            Add("Activation de « {0} »…", "Enabling “{0}”…", "„{0}“ wird aktiviert…", "Activando «{0}»…");
            Add("Désactivation de « {0} »…", "Disabling “{0}”…", "„{0}“ wird deaktiviert…", "Desactivando «{0}»…");
            Add("Activation en cours.", "Enabling.", "Aktivierung läuft.", "Activando.");
            Add("Désactivation en cours.", "Disabling.", "Deaktivierung läuft.", "Desactivando.");
            Add("Erreur : {0}", "Error: {0}", "Fehler: {0}", "Error: {0}");
            Add("Impossible de modifier l'état de la carte.",
                "Unable to change the card status.",
                "Der Kartenstatus konnte nicht geändert werden.",
                "No se pudo cambiar el estado de la tarjeta.");
            Add("Droits administrateur requis", "Administrator rights required", "Administratorrechte erforderlich", "Se requieren permisos de administrador");
            Add("Relancer en administrateur", "Restart as administrator", "Als Administrator neu starten", "Reiniciar como administrador");
            Add("Windows n'a pas accordé les droits administrateur à cette session de l'application.\n\nSans ces droits, les changements d'adresse IP et l'activation des cartes sont bloqués.\n\nSouhaitez-vous relancer Easy IP-Switch en tant qu'administrateur ?",
                "Windows did not grant administrator rights to this application session.\n\nWithout these rights, IP address changes and card activation are blocked.\n\nDo you want to restart Easy IP-Switch as administrator?",
                "Windows hat dieser Anwendungssitzung keine Administratorrechte erteilt.\n\nOhne diese Rechte sind Änderungen der IP-Adresse und die Aktivierung von Karten blockiert.\n\nMöchten Sie Easy IP-Switch als Administrator neu starten?",
                "Windows no concedió permisos de administrador a esta sesión de la aplicación.\n\nSin estos permisos, los cambios de dirección IP y la activación de tarjetas están bloqueados.\n\n¿Desea reiniciar Easy IP-Switch como administrador?");
            Add("\n\nAttention : les modifications non appliquées seront perdues.",
                "\n\nWarning: unapplied changes will be lost.",
                "\n\nAchtung: Nicht übernommene Änderungen gehen verloren.",
                "\n\nAtención: los cambios no aplicados se perderán.");
            Add("Impossible de relancer en administrateur : {0}",
                "Unable to restart as administrator: {0}",
                "Neustart als Administrator nicht möglich: {0}",
                "No se pudo reiniciar como administrador: {0}");
            Add("Ouverture des Connexions réseau et préparation du raccourci…",
                "Opening Network Connections and preparing the shortcut…",
                "Netzwerkverbindungen werden geöffnet und die Verknüpfung vorbereitet…",
                "Abriendo Conexiones de red y preparando el acceso directo…");
            Add("Ouvre les Connexions réseau Windows.\r\nRaccourci : {0}",
                "Opens Windows Network Connections.\r\nShortcut: {0}",
                "Öffnet die Windows-Netzwerkverbindungen.\r\nVerknüpfung: {0}",
                "Abre las Conexiones de red de Windows.\r\nAcceso directo: {0}");
            Add("Impossible d'ouvrir les Connexions réseau : {0}",
                "Unable to open Network Connections: {0}",
                "Netzwerkverbindungen konnten nicht geöffnet werden: {0}",
                "No se pudieron abrir las Conexiones de red: {0}");
            Add("Impossible de lancer l'opération : {0}",
                "Unable to start the operation: {0}",
                "Der Vorgang konnte nicht gestartet werden: {0}",
                "No se pudo iniciar la operación: {0}");
            Add("Impossible d'ouvrir le lien : {0}",
                "Unable to open the link: {0}",
                "Der Link konnte nicht geöffnet werden: {0}",
                "No se pudo abrir el enlace: {0}");

            // Cartes réseau
            Add("Carte active", "Card enabled", "Karte aktiv", "Tarjeta activa");
            Add("Adresse IP", "IP address", "IP-Adresse", "Dirección IP");
            Add("Masque", "Mask", "Maske", "Máscara");
            Add("Passerelle", "Gateway", "Gateway", "Puerta de enlace");
            Add("Activer ou désactiver la carte réseau",
                "Enable or disable the network card",
                "Netzwerkkarte aktivieren oder deaktivieren",
                "Activar o desactivar la tarjeta de red");
            Add("Adresse fixe", "Static address", "Feste Adresse", "Dirección fija");
            Add("Choisir entre l'attribution automatique et une adresse fixe",
                "Choose between automatic assignment and a static address",
                "Zwischen automatischer Zuweisung und fester Adresse wählen",
                "Elegir entre asignación automática y dirección fija");
            Add("ex. 192.168.1.50", "e.g. 192.168.1.50", "z. B. 192.168.1.50", "p. ej. 192.168.1.50");
            Add("ex. 192.168.1.1", "e.g. 192.168.1.1", "z. B. 192.168.1.1", "p. ej. 192.168.1.1");
            Add("Appliquer la configuration de cette carte",
                "Apply this card's configuration",
                "Konfiguration dieser Karte übernehmen",
                "Aplicar la configuración de esta tarjeta");
            Add("Revenir à la configuration actuelle de cette carte",
                "Return to this card's current configuration",
                "Zur aktuellen Konfiguration dieser Karte zurückkehren",
                "Volver a la configuración actual de esta tarjeta");
            Add("Vérifier que l'adresse IP de cette carte et sa passerelle répondent",
                "Check that this card's IP address and gateway respond",
                "Prüfen, ob die IP-Adresse dieser Karte und ihr Gateway antworten",
                "Comprobar que la dirección IP de esta tarjeta y su puerta de enlace responden");
            Add("Vérifiez la saisie", "Check your entries", "Eingaben prüfen", "Compruebe los datos");
            Add("Adresse IPv4 de la carte, par exemple 192.168.1.50",
                "Card IPv4 address, for example 192.168.1.50",
                "IPv4-Adresse der Karte, zum Beispiel 192.168.1.50",
                "Dirección IPv4 de la tarjeta, por ejemplo 192.168.1.50");
            Add("Masque de sous-réseau, par exemple 255.255.255.0",
                "Subnet mask, for example 255.255.255.0",
                "Subnetzmaske, zum Beispiel 255.255.255.0",
                "Máscara de subred, por ejemplo 255.255.255.0");
            Add("Passerelle par défaut, par exemple 192.168.1.1 (facultative)",
                "Default gateway, for example 192.168.1.1 (optional)",
                "Standard-Gateway, zum Beispiel 192.168.1.1 (optional)",
                "Puerta de enlace predeterminada, por ejemplo 192.168.1.1 (opcional)");
            Add("Adresse IP invalide. Format attendu : 192.168.1.50",
                "Invalid IP address. Expected format: 192.168.1.50",
                "Ungültige IP-Adresse. Erwartetes Format: 192.168.1.50",
                "Dirección IP no válida. Formato esperado: 192.168.1.50");
            Add("Adresse IP invalide : 0.0.0.0 et 255.255.255.255 sont interdites.",
                "Invalid IP address: 0.0.0.0 and 255.255.255.255 are not allowed.",
                "Ungültige IP-Adresse: 0.0.0.0 und 255.255.255.255 sind nicht zulässig.",
                "Dirección IP no válida: 0.0.0.0 y 255.255.255.255 no están permitidas.");
            Add("Adresse 169.254.x.x réservée par Windows : choisissez une autre adresse.",
                "169.254.x.x address reserved by Windows: choose another address.",
                "Die Adresse 169.254.x.x ist von Windows reserviert: Wählen Sie eine andere Adresse.",
                "Dirección 169.254.x.x reservada por Windows: elija otra dirección.");
            Add("Masque invalide. Format attendu : 255.255.255.0",
                "Invalid mask. Expected format: 255.255.255.0",
                "Ungültige Maske. Erwartetes Format: 255.255.255.0",
                "Máscara no válida. Formato esperado: 255.255.255.0");
            Add("Masque non contigu : utilisez par exemple 255.255.255.0 ou 255.255.0.0.",
                "Non-contiguous mask: use for example 255.255.255.0 or 255.255.0.0.",
                "Nicht zusammenhängende Maske: Verwenden Sie zum Beispiel 255.255.255.0 oder 255.255.0.0.",
                "Máscara no contigua: use por ejemplo 255.255.255.0 o 255.255.0.0.");
            Add("Passerelle invalide. Format attendu : 192.168.1.1",
                "Invalid gateway. Expected format: 192.168.1.1",
                "Ungültiges Gateway. Erwartetes Format: 192.168.1.1",
                "Puerta de enlace no válida. Formato esperado: 192.168.1.1");
            Add("La passerelle doit être différente de l'adresse IP.",
                "The gateway must be different from the IP address.",
                "Das Gateway muss sich von der IP-Adresse unterscheiden.",
                "La puerta de enlace debe ser diferente de la dirección IP.");
            Add("La passerelle n'est pas dans le sous-réseau {0}.",
                "The gateway is not in subnet {0}.",
                "Das Gateway befindet sich nicht im Subnetz {0}.",
                "La puerta de enlace no está en la subred {0}.");
            Add("État de la carte indisponible", "Card status unavailable", "Kartenstatus nicht verfügbar", "Estado de la tarjeta no disponible");
            Add("Carte désactivée", "Card disabled", "Karte deaktiviert", "Tarjeta desactivada");
            Add("Adresse attribuée automatiquement par le réseau",
                "Address assigned automatically by the network",
                "Vom Netzwerk automatisch zugewiesene Adresse",
                "Dirección asignada automáticamente por la red");
            Add("Configuration fixe en attente", "Static configuration pending", "Feste Konfiguration ausstehend", "Configuración fija pendiente");
            Add("Adresse configurée manuellement", "Manually configured address", "Manuell konfigurierte Adresse", "Dirección configurada manualmente");
            Add("Test…", "Testing…", "Test läuft…", "Probando…");
            Add("Cette carte n'a pas d'adresse IPv4 à tester.",
                "This card has no IPv4 address to test.",
                "Diese Karte hat keine IPv4-Adresse zum Testen.",
                "Esta tarjeta no tiene una dirección IPv4 para probar.");
            Add("Test de « {0} » en cours…", "Testing “{0}”…", "„{0}“ wird getestet…", "Probando «{0}»…");
            Add("Le test a échoué.", "The test failed.", "Der Test ist fehlgeschlagen.", "La prueba falló.");
            Add("Aucune réponse de l'adresse {0}.",
                "No response from address {0}.",
                "Keine Antwort von der Adresse {0}.",
                "Sin respuesta de la dirección {0}.");
            Add("L'adresse {0} répond ({1} ms). Aucune passerelle définie.",
                "Address {0} responds ({1} ms). No gateway defined.",
                "Die Adresse {0} antwortet ({1} ms). Kein Gateway definiert.",
                "La dirección {0} responde ({1} ms). No hay puerta de enlace definida.");
            Add("Adresse et passerelle joignables ({0} ms).",
                "Address and gateway reachable ({0} ms).",
                "Adresse und Gateway erreichbar ({0} ms).",
                "Dirección y puerta de enlace accesibles ({0} ms).");
            Add("L'adresse répond, mais la passerelle {0} ne répond pas.",
                "The address responds, but gateway {0} does not.",
                "Die Adresse antwortet, aber das Gateway {0} antwortet nicht.",
                "La dirección responde, pero la puerta de enlace {0} no responde.");
            Add("Adresse fixe enregistrée : {0} ; DHCP actif, carte non connectée.",
                "Static address saved: {0}; DHCP active, card not connected.",
                "Feste Adresse gespeichert: {0}; DHCP aktiv, Karte nicht verbunden.",
                "Dirección fija guardada: {0}; DHCP activo, tarjeta no conectada.");
            Add("DHCP actif ; adresse fixe enregistrée : {0}",
                "DHCP active; static address saved: {0}",
                "DHCP aktiv; feste Adresse gespeichert: {0}",
                "DHCP activo; dirección fija guardada: {0}");
            Add("Mode fixe non confirmé", "Static mode not confirmed", "Fester Modus nicht bestätigt", "Modo fijo no confirmado");
            Add("L'adresse est enregistrée, mais Windows indique que le DHCP est encore actif.",
                "The address is saved, but Windows reports that DHCP is still active.",
                "Die Adresse ist gespeichert, aber Windows meldet, dass DHCP noch aktiv ist.",
                "La dirección está guardada, pero Windows indica que DHCP sigue activo.");
            Add("Actualisez pour vérifier l'état de cette carte.",
                "Refresh to check this card's status.",
                "Aktualisieren Sie, um den Status dieser Karte zu prüfen.",
                "Actualice para comprobar el estado de esta tarjeta.");
            Add("Activez la carte pour modifier sa configuration.",
                "Enable the card to change its configuration.",
                "Aktivieren Sie die Karte, um ihre Konfiguration zu ändern.",
                "Active la tarjeta para cambiar su configuración.");
            Add("Modification non appliquée", "Change not applied", "Änderung nicht übernommen", "Cambio no aplicado");
            Add("Cliquez sur Changer pour appliquer cette configuration.",
                "Click Apply to apply this configuration.",
                "Klicken Sie auf Übernehmen, um diese Konfiguration anzuwenden.",
                "Haga clic en Aplicar para aplicar esta configuración.");

            // Ping
            Add("Tester une adresse IP sur le réseau", "Test an IP address on the network", "Eine IP-Adresse im Netzwerk testen", "Probar una dirección IP en la red");
            Add("Choisissez la carte qui doit envoyer le test, puis saisissez une adresse IPv4 à joindre.",
                "Choose the card that should send the test, then enter an IPv4 address to reach.",
                "Wählen Sie die Karte, die den Test senden soll, und geben Sie eine zu erreichende IPv4-Adresse ein.",
                "Elija la tarjeta que debe enviar la prueba y escriba una dirección IPv4 a la que llegar.");
            Add("Carte réseau (source)", "Network card (source)", "Netzwerkkarte (Quelle)", "Tarjeta de red (origen)");
            Add("Adresse IPv4 à tester", "IPv4 address to test", "Zu testende IPv4-Adresse", "Dirección IPv4 a probar");
            Add("Journal des tests", "Test log", "Testprotokoll", "Registro de pruebas");
            Add("Aucun test lancé pour le moment.", "No test started yet.", "Noch kein Test gestartet.", "Aún no se ha iniciado ninguna prueba.");
            Add("Ping", "Ping", "Ping", "Ping");
            Add("Effacer", "Clear", "Löschen", "Borrar");
            Add("Journal effacé.", "Log cleared.", "Protokoll gelöscht.", "Registro borrado.");
            Add("Aucune carte avec adresse IPv4", "No card with an IPv4 address", "Keine Karte mit IPv4-Adresse", "Ninguna tarjeta con dirección IPv4");
            Add("Aucune carte disponible", "No card available", "Keine Karte verfügbar", "Ninguna tarjeta disponible");
            Add("(fixe)", "(static)", "(fest)", "(fija)");
            Add("Aucune carte réseau avec une adresse IPv4 n'est disponible.",
                "No network card with an IPv4 address is available.",
                "Keine Netzwerkkarte mit einer IPv4-Adresse verfügbar.",
                "No hay ninguna tarjeta de red con una dirección IPv4 disponible.");
            Add("Adresse invalide : utilisez le format 192.168.1.1",
                "Invalid address: use the format 192.168.1.1",
                "Ungültige Adresse: Verwenden Sie das Format 192.168.1.1",
                "Dirección no válida: use el formato 192.168.1.1");
            Add("Cette adresse ne peut pas être testée.", "This address cannot be tested.", "Diese Adresse kann nicht getestet werden.", "Esta dirección no se puede probar.");
            Add("La carte sélectionnée n'a pas d'adresse IPv4.",
                "The selected card has no IPv4 address.",
                "Die ausgewählte Karte hat keine IPv4-Adresse.",
                "La tarjeta seleccionada no tiene dirección IPv4.");
            Add("Ping en cours vers {0} via {1}…", "Pinging {0} via {1}…", "Ping an {0} über {1}…", "Haciendo ping a {0} mediante {1}…");
            Add("Échec du test vers {0} : {1}", "Test to {0} failed: {1}", "Test zu {0} fehlgeschlagen: {1}", "La prueba a {0} falló: {1}");
            Add("Le test a échoué : {0}", "The test failed: {0}", "Der Test ist fehlgeschlagen: {0}", "La prueba falló: {0}");
            Add("Réponse de {0} {1}.", "Response from {0} {1}.", "Antwort von {0} {1}.", "Respuesta de {0} {1}.");
            Add("Aucune réponse de {0} : {1}.", "No response from {0}: {1}.", "Keine Antwort von {0}: {1}.", "Sin respuesta de {0}: {1}.");

            // Services Windows
            Add("Wi-Fi", "Wi-Fi", "WLAN", "Wi-Fi");
            Add("Ethernet (câble)", "Ethernet (cable)", "Ethernet (Kabel)", "Ethernet (cable)");
            Add("Connectée", "Connected", "Verbunden", "Conectada");
            Add("Non connectée", "Not connected", "Nicht verbunden", "No conectada");
            Add("Désactivée", "Disabled", "Deaktiviert", "Desactivada");
            Add("État indisponible", "Status unavailable", "Status nicht verfügbar", "Estado no disponible");
            Add("Lecture des cartes réseau impossible. {0}",
                "Unable to read the network cards. {0}",
                "Netzwerkkarten konnten nicht gelesen werden. {0}",
                "No se pudieron leer las tarjetas de red. {0}");
            Add("DHCP activé. Adresse obtenue : {0}",
                "DHCP enabled. Address obtained: {0}",
                "DHCP aktiviert. Erhaltene Adresse: {0}",
                "DHCP activado. Dirección obtenida: {0}");
            Add("DHCP activé. En attente d'une adresse du serveur DHCP.",
                "DHCP enabled. Waiting for an address from the DHCP server.",
                "DHCP aktiviert. Warten auf eine Adresse vom DHCP-Server.",
                "DHCP activado. Esperando una dirección del servidor DHCP.");
            Add("Impossible d'activer le DHCP. {0}",
                "Unable to enable DHCP. {0}",
                "DHCP konnte nicht aktiviert werden. {0}",
                "No se pudo activar DHCP. {0}");
            Add("Impossible d'activer la carte.",
                "Unable to enable the card.",
                "Karte konnte nicht aktiviert werden.",
                "No se pudo activar la tarjeta.");
            Add("Impossible de désactiver la carte.",
                "Unable to disable the card.",
                "Karte konnte nicht deaktiviert werden.",
                "No se pudo desactivar la tarjeta.");
            Add("Identifiant de carte indisponible. Actualisez la liste et réessayez.",
                "Card identifier unavailable. Refresh the list and try again.",
                "Kartenkennung nicht verfügbar. Aktualisieren Sie die Liste und versuchen Sie es erneut.",
                "Identificador de tarjeta no disponible. Actualice la lista e inténtelo de nuevo.");
            Add("La demande a été refusée par Windows.",
                "The request was refused by Windows.",
                "Die Anforderung wurde von Windows abgelehnt.",
                "Windows rechazó la solicitud.");
            Add("Demande envoyée. L'état de la carte n'est pas encore confirmé ; une nouvelle actualisation sera effectuée.",
                "Request sent. The card status is not confirmed yet; another refresh will be performed.",
                "Anforderung gesendet. Der Kartenstatus ist noch nicht bestätigt; eine weitere Aktualisierung wird durchgeführt.",
                "Solicitud enviada. El estado de la tarjeta aún no está confirmado; se realizará otra actualización.");
            Add("Erreur Windows inconnue.", "Unknown Windows error.", "Unbekannter Windows-Fehler.", "Error de Windows desconocido.");
            Add("Windows n'a pas confirmé la demande.",
                "Windows did not confirm the request.",
                "Windows hat die Anforderung nicht bestätigt.",
                "Windows no confirmó la solicitud.");
            Add("Windows a refusé la demande (code WMI {0}).",
                "Windows refused the request (WMI code {0}).",
                "Windows hat die Anforderung abgelehnt (WMI-Code {0}).",
                "Windows rechazó la solicitud (código WMI {0}).");
            Add("Identifiant de carte indisponible. Actualisez la liste.",
                "Card identifier unavailable. Refresh the list.",
                "Kartenkennung nicht verfügbar. Aktualisieren Sie die Liste.",
                "Identificador de tarjeta no disponible. Actualice la lista.");
            Add("Carte introuvable dans Windows. Actualisez la liste puis réessayez.",
                "Card not found in Windows. Refresh the list and try again.",
                "Karte in Windows nicht gefunden. Aktualisieren Sie die Liste und versuchen Sie es erneut.",
                "No se encontró la tarjeta en Windows. Actualice la lista e inténtelo de nuevo.");
            Add("Connexions réseau ouvertes ; raccourci « {0} » créé sur le Bureau.",
                "Network Connections opened; shortcut “{0}” created on the desktop.",
                "Netzwerkverbindungen geöffnet; Verknüpfung „{0}“ auf dem Desktop erstellt.",
                "Conexiones de red abiertas; acceso directo «{0}» creado en el escritorio.");
            Add("Connexions réseau ouvertes ; raccourci « {0} » déjà présent sur le Bureau.",
                "Network Connections opened; shortcut “{0}” already on the desktop.",
                "Netzwerkverbindungen geöffnet; Verknüpfung „{0}“ bereits auf dem Desktop.",
                "Conexiones de red abiertas; el acceso directo «{0}» ya está en el escritorio.");
            Add("Connexions réseau ouvertes, mais raccourci impossible à créer : {0}",
                "Network Connections opened, but the shortcut could not be created: {0}",
                "Netzwerkverbindungen geöffnet, aber die Verknüpfung konnte nicht erstellt werden: {0}",
                "Conexiones de red abiertas, pero no se pudo crear el acceso directo: {0}");
            Add("Raccourci disponible sur le Bureau, mais ouverture impossible : {0}",
                "Shortcut available on the desktop, but it could not be opened: {0}",
                "Verknüpfung auf dem Desktop verfügbar, aber Öffnen nicht möglich: {0}",
                "Acceso directo disponible en el escritorio, pero no se pudo abrir: {0}");
            Add("Impossible de créer le raccourci : {0} Ouverture impossible : {1}",
                "Unable to create the shortcut: {0} Unable to open: {1}",
                "Verknüpfung konnte nicht erstellt werden: {0} Öffnen nicht möglich: {1}",
                "No se pudo crear el acceso directo: {0} No se pudo abrir: {1}");
            Add("Windows n'a pas confirmé la création du raccourci.",
                "Windows did not confirm the shortcut creation.",
                "Windows hat die Erstellung der Verknüpfung nicht bestätigt.",
                "Windows no confirmó la creación del acceso directo.");
            Add("Le dossier Bureau est introuvable.", "The Desktop folder could not be found.", "Der Desktop-Ordner wurde nicht gefunden.", "No se encontró la carpeta Escritorio.");
            Add("Aucun nom libre pour le raccourci sur le Bureau.",
                "No free name for the desktop shortcut.",
                "Kein freier Name für die Desktop-Verknüpfung.",
                "No hay ningún nombre libre para el acceso directo del escritorio.");
            Add("Ouvrir les connexions réseau Windows",
                "Open Windows network connections",
                "Windows-Netzwerkverbindungen öffnen",
                "Abrir las conexiones de red de Windows");
            Add("Carte réseau introuvable. Actualisez la liste avant de réessayer.",
                "Network card not found. Refresh the list before trying again.",
                "Netzwerkkarte nicht gefunden. Aktualisieren Sie die Liste, bevor Sie es erneut versuchen.",
                "No se encontró la tarjeta de red. Actualice la lista antes de volver a intentarlo.");
            Add("Configuration IPv4 invalide. Vérifiez l'adresse, le masque et la passerelle.",
                "Invalid IPv4 configuration. Check the address, mask and gateway.",
                "Ungültige IPv4-Konfiguration. Prüfen Sie Adresse, Maske und Gateway.",
                "Configuración IPv4 no válida. Compruebe la dirección, la máscara y la puerta de enlace.");
            Add("Impossible de confirmer la commande Windows : {0} Vérifiez l'état actuel avant de réessayer.",
                "Unable to confirm the Windows command: {0} Check the current status before trying again.",
                "Der Windows-Befehl konnte nicht bestätigt werden: {0} Prüfen Sie den aktuellen Status, bevor Sie es erneut versuchen.",
                "No se pudo confirmar el comando de Windows: {0} Compruebe el estado actual antes de volver a intentarlo.");
            Add("Windows n'a pas confirmé la commande. Vérifiez l'état actuel avant de réessayer.",
                "Windows did not confirm the command. Check the current status before trying again.",
                "Windows hat den Befehl nicht bestätigt. Prüfen Sie den aktuellen Status, bevor Sie es erneut versuchen.",
                "Windows no confirmó el comando. Compruebe el estado actual antes de volver a intentarlo.");
            Add("Délai dépassé : la configuration a peut-être changé. Actualisez avant de réessayer.",
                "Timed out: the configuration may have changed. Refresh before trying again.",
                "Zeitüberschreitung: Die Konfiguration hat sich möglicherweise geändert. Aktualisieren Sie, bevor Sie es erneut versuchen.",
                "Se agotó el tiempo: la configuración puede haber cambiado. Actualice antes de volver a intentarlo.");
            Add("Code Windows : {0}.", "Windows code: {0}.", "Windows-Code: {0}.", "Código de Windows: {0}.");
            Add("Impossible d'appliquer l'adresse fixe. {0} Vérifiez l'état actuel avant de réessayer.",
                "Unable to apply the static address. {0} Check the current status before trying again.",
                "Die feste Adresse konnte nicht angewendet werden. {0} Prüfen Sie den aktuellen Status, bevor Sie es erneut versuchen.",
                "No se pudo aplicar la dirección fija. {0} Compruebe el estado actual antes de volver a intentarlo.");
            Add("Adresse fixe appliquée : {0}", "Static address applied: {0}", "Feste Adresse angewendet: {0}", "Dirección fija aplicada: {0}");
            Add("Adresse fixe enregistrée : {0}. Carte non connectée ; vérifiez la liaison après branchement.",
                "Static address saved: {0}. Card not connected; check the link after connecting.",
                "Feste Adresse gespeichert: {0}. Karte nicht verbunden; prüfen Sie die Verbindung nach dem Anschließen.",
                "Dirección fija guardada: {0}. Tarjeta no conectada; compruebe el enlace tras conectarla.");
            Add("DHCP encore actif", "DHCP still active", "DHCP noch aktiv", "DHCP aún activo");
            Add("DHCP encore enregistré", "DHCP still saved", "DHCP noch gespeichert", "DHCP aún guardado");
            Add("Adresse fixe enregistrée : {0}. Carte non connectée ; {1}. Vérifiez après branchement.",
                "Static address saved: {0}. Card not connected; {1}. Check after connecting.",
                "Feste Adresse gespeichert: {0}. Karte nicht verbunden; {1}. Prüfen Sie nach dem Anschließen.",
                "Dirección fija guardada: {0}. Tarjeta no conectada; {1}. Compruebe tras conectarla.");
            Add("Adresse fixe enregistrée, mais Windows indique encore DHCP actif. Actualisez et vérifiez la liaison avant de réessayer.",
                "Static address saved, but Windows still reports DHCP active. Refresh and check the link before trying again.",
                "Feste Adresse gespeichert, aber Windows meldet weiterhin aktives DHCP. Aktualisieren Sie und prüfen Sie die Verbindung, bevor Sie es erneut versuchen.",
                "Dirección fija guardada, pero Windows sigue indicando DHCP activo. Actualice y compruebe el enlace antes de volver a intentarlo.");
            Add("Configuration incomplète : Windows indique que le DHCP est encore actif ou enregistré.",
                "Incomplete configuration: Windows reports that DHCP is still active or saved.",
                "Unvollständige Konfiguration: Windows meldet, dass DHCP noch aktiv oder gespeichert ist.",
                "Configuración incompleta: Windows indica que DHCP sigue activo o guardado.");
            Add(" Carte non connectée : branchez le câble, actualisez puis réessayez.",
                " Card not connected: connect the cable, refresh and try again.",
                " Karte nicht verbunden: Schließen Sie das Kabel an, aktualisieren Sie und versuchen Sie es erneut.",
                " Tarjeta no conectada: conecte el cable, actualice y vuelva a intentarlo.");
            Add(" Actualisez et vérifiez la carte avant de réessayer.",
                " Refresh and check the card before trying again.",
                " Aktualisieren und prüfen Sie die Karte, bevor Sie es erneut versuchen.",
                " Actualice y compruebe la tarjeta antes de volver a intentarlo.");
            Add("Windows indique que les paramètres enregistrés ne correspondent pas à la demande. Actualisez avant de réessayer.",
                "Windows reports that the saved settings do not match the request. Refresh before trying again.",
                "Windows meldet, dass die gespeicherten Einstellungen nicht der Anforderung entsprechen. Aktualisieren Sie, bevor Sie es erneut versuchen.",
                "Windows indica que la configuración guardada no coincide con la solicitud. Actualice antes de volver a intentarlo.");
            Add("Demande envoyée, mais les paramètres IPv4 n'ont pas pu être entièrement confirmés. Actualisez avant de réessayer.",
                "Request sent, but the IPv4 settings could not be fully confirmed. Refresh before trying again.",
                "Anforderung gesendet, aber die IPv4-Einstellungen konnten nicht vollständig bestätigt werden. Aktualisieren Sie, bevor Sie es erneut versuchen.",
                "Solicitud enviada, pero no se pudieron confirmar completamente los ajustes IPv4. Actualice antes de volver a intentarlo.");

            // Ping bas niveau et erreurs
            Add("Adresse vide.", "Empty address.", "Leere Adresse.", "Dirección vacía.");
            Add("Réponse en {0} ms", "Response in {0} ms", "Antwort in {0} ms", "Respuesta en {0} ms");
            Add("Délai dépassé", "Timed out", "Zeitüberschreitung", "Tiempo agotado");
            Add("Délai dépassé.", "Timed out.", "Zeitüberschreitung.", "Tiempo agotado.");
            Add("Impossible de démarrer netsh.", "Unable to start netsh.", "netsh konnte nicht gestartet werden.", "No se pudo iniciar netsh.");
            Add("Aucune réponse", "No response", "Keine Antwort", "Sin respuesta");
            Add("Hôte inaccessible", "Host unreachable", "Host nicht erreichbar", "Host inaccesible");
            Add("Hôte introuvable", "Host not found", "Host nicht gefunden", "Host no encontrado");
            Add("Réseau inaccessible", "Network unreachable", "Netzwerk nicht erreichbar", "Red inaccesible");
            Add("Réponse", "Response", "Antwort", "Respuesta");

            // Erreurs globales
            Add("Une erreur inattendue est survenue :\r\n\r\n{0}",
                "An unexpected error occurred:\r\n\r\n{0}",
                "Ein unerwarteter Fehler ist aufgetreten:\r\n\r\n{0}",
                "Se produjo un error inesperado:\r\n\r\n{0}");
            Add("Exception inconnue", "Unknown exception", "Unbekannte Ausnahme", "Excepción desconocida");
        }
    }
}
