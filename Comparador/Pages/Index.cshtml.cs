
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.RegularExpressions;


using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ComparadorUsusi2.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        public List<ResultadosComparacion> Resultados { get; set; } = new List<ResultadosComparacion>();
        public string LogMessages { get; set; } = "Log de operaciones:\n";

        // Diccionario de mapeo para casos espec�ficos de niveles
        private readonly Dictionary<string, string> mapeoNiveles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DARI- T", "DARI_T" },
            { "DCOCI- DCI", "DCOCI-DCI" },
            { "DNSR-BR", "DNSRCO-BR" },
            { "JPGCO-BDSR", "JPGCO-BR" }, // Tambi�n podr�a ser "JPOCO-BDSR"
            { "MCDO-DIVIN", "MDCO-DIVIN" }
        };

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            // L�gica para manejar la carga inicial si es necesario
        }

        public IActionResult OnPostCompararArchivos(IFormFile file1, IFormFile file2)
        {
            if (file1 == null || file2 == null)
            {
                ModelState.AddModelError(string.Empty, "Por favor, seleccione ambos archivos.");
                return Page();
            }

            try
            {
                LogMessages = "Log de operaciones:\n";
                Resultados = new List<ResultadosComparacion>();

                // Guardar archivos temporalmente
                var file1Path = Path.GetTempFileName();
                var file2Path = Path.GetTempFileName();

                using (var stream1 = new FileStream(file1Path, FileMode.Create))
                {
                    file1.CopyTo(stream1);
                }

                using (var stream2 = new FileStream(file2Path, FileMode.Create))
                {
                    file2.CopyTo(stream2);
                }

                LogMessages += $"Archivos cargados correctamente.\n";

                // Obtener usuarios de referencia desde el archivo 1 (todos los usuarios)
                var usuariosReferencia = ObtenerUsuariosReferencia(file1Path);
                LogMessages += $"Se encontraron {usuariosReferencia.Count} usuarios en el archivo de referencia.\n";

                // Comparar con el archivo 2 (usuarios por nivel)
                CompararArchivos(file2Path, usuariosReferencia);

                // Eliminar archivos temporales
                System.IO.File.Delete(file1Path);
                System.IO.File.Delete(file2Path);

                LogMessages += $"Comparaci�n finalizada. Se encontraron {Resultados.Count} inconsistencias.\n";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al comparar archivos.");
                ModelState.AddModelError(string.Empty, "Error al comparar archivos: " + ex.Message);
                LogMessages += $"ERROR: {ex.Message}\n";
                return Page();
            }
        }

        private Dictionary<string, UsuarioReferencia> ObtenerUsuariosReferencia(string filePath)
        {
            var usuarios = new Dictionary<string, UsuarioReferencia>();
            LogMessages += "Procesando archivo de referencia (hoja �nica)...\n";

            using (var pkg = new ExcelPackage(new FileInfo(filePath)))
            {
                var hoja = pkg.Workbook.Worksheets[0];
                if (hoja.Dimension == null)
                {
                    LogMessages += "La hoja est� vac�a o no tiene datos.\n";
                    return usuarios;
                }

                int lastRow = hoja.Dimension.End.Row;

                // Encontrar �ndices de columnas por nombre
                int colNombre = -1, colCedula = -1, colNivel = -1, colEstado = -1;

                for (int col = 1; col <= hoja.Dimension.End.Column; col++)
                {
                    string headerText = hoja.Cells[1, col].Text.Trim().ToLower();
                    if (headerText.Contains("nombre")) colNombre = col;
                    else if (headerText.Contains("c.i") || headerText.Contains("cedula") || headerText.Contains("c�dula")) colCedula = col;
                    else if (headerText.Contains("nivel")) colNivel = col;
                    else if (headerText.Contains("permiso") || headerText.Contains("estado") || headerText.Contains("activo")) colEstado = col;
                }

                if (colNombre == -1 || colCedula == -1 || colNivel == -1 || colEstado == -1)
                {
                    LogMessages += "No se encontraron todas las columnas necesarias en el archivo de referencia.\n";
                    LogMessages += $"Columnas encontradas: Nombre={colNombre}, CI={colCedula}, Nivel={colNivel}, Estado={colEstado}\n";
                    throw new Exception("Formato de archivo incorrecto. No se encontraron todas las columnas necesarias.");
                }

                for (int fila = 2; fila <= lastRow; fila++)
                {
                    string nombre = hoja.Cells[fila, colNombre].Text.Trim();
                    string cedula = hoja.Cells[fila, colCedula].Text.Trim();
                    string nivel = hoja.Cells[fila, colNivel].Text.Trim();
                    string estado = hoja.Cells[fila, colEstado].Text.Trim().ToLower();

                    if (string.IsNullOrWhiteSpace(cedula))
                        continue;

                    // Limpiar la c�dula para tener solo n�meros
                    cedula = Regex.Replace(cedula, @"[^\d]", "");

                    if (EsCedulaValida(cedula))
                    {
                        // CORRECCI�N: Verificar correctamente si el usuario est� activo
                        bool estaActivo = estado.Equals("activo", StringComparison.OrdinalIgnoreCase);

                        // Agregar depuraci�n para el usuario espec�fico
                        if (cedula == "48456988") // Dami�n TEJERA RECALDE
                        {
                            LogMessages += $"DEPURACI�N USUARIO 48456988: Estado original='{estado}', estaActivo={estaActivo}\n";
                        }

                        usuarios[cedula] = new UsuarioReferencia
                        {
                            Cedula = cedula,
                            Nombre = nombre,
                            EstaActivo = estaActivo,
                            Nivel = nivel.Trim()
                        };

                        LogMessages += $"Usuario encontrado: {cedula} - {nombre} - {nivel} - {(estaActivo ? "Activo" : "Inactivo")}\n";
                    }
                    else
                    {
                        LogMessages += $"C�dula inv�lida ignorada: {cedula} en fila {fila}\n";
                    }
                }
            }

            LogMessages += $"Total de usuarios procesados del archivo de referencia: {usuarios.Count}\n";
            return usuarios;
        }

        private void CompararArchivos(string file2Path, Dictionary<string, UsuarioReferencia> usuariosReferencia)
        {
            LogMessages += "Iniciando comparaci�n con archivo de niveles usando m�todo mejorado...\n";

            // Para rastrear usuarios encontrados y sus niveles
            var usuariosEncontrados = new Dictionary<string, List<string>>();
            var usuariosInactivosEncontrados = new HashSet<string>();

            // Conjunto de niveles disponibles en el archivo 2 (nombres de hojas)
            var nivelesDisponibles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Mapeo de niveles normalizados a nombres originales de hojas
            var mapeoNivelesNormalizados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var pkg = new ExcelPackage(new FileInfo(file2Path)))
            {
                // Registrar todas las hojas disponibles como niveles v�lidos
                foreach (var hoja in pkg.Workbook.Worksheets)
                {
                    string nombreHoja = hoja.Name.Trim();

                    // Ignorar la hoja "i2-usuarios" u otras hojas de configuraci�n
                    if (nombreHoja.Equals("i2-usuarios", StringComparison.OrdinalIgnoreCase))
                        continue;

                    nivelesDisponibles.Add(nombreHoja);

                    // Guardar tambi�n la versi�n normalizada para b�squedas flexibles
                    string nombreNormalizado = NormalizarNivel(nombreHoja);
                    mapeoNivelesNormalizados[nombreNormalizado] = nombreHoja;
                }

                LogMessages += $"Niveles disponibles en archivo 2: {string.Join(", ", nivelesDisponibles)}\n";

                // Procesar cada hoja
                foreach (var hoja in pkg.Workbook.Worksheets)
                {
                    if (hoja.Dimension == null) continue;

                    string nombreHoja = hoja.Name.Trim();

                    // Ignorar la hoja "i2-usuarios"
                    if (nombreHoja.Equals("i2-usuarios", StringComparison.OrdinalIgnoreCase))
                    {
                        LogMessages += $"Ignorando hoja {nombreHoja} seg�n configuraci�n...\n";
                        continue;
                    }

                    LogMessages += $"Procesando hoja: {nombreHoja}\n";

                    int lastRow = hoja.Dimension.End.Row;
                    int lastCol = hoja.Dimension.End.Column;

                    // M�TODO MEJORADO: Buscar c�dulas en toda la hoja
                    var cedulasEncontradas = new HashSet<string>();
                    var cedulasConFila = new Dictionary<string, int>(); // Para rastrear la fila donde se encontr� cada c�dula

                    // Escanear toda la hoja buscando secuencias de 8 d�gitos consecutivos
                    for (int fila = 1; fila <= lastRow; fila++)
                    {
                        for (int col = 1; col <= lastCol; col++)
                        {
                            string cellValue = hoja.Cells[fila, col].Text.Trim();

                            // Extraer todas las secuencias de 7-10 d�gitos (posibles c�dulas)
                            var matches = Regex.Matches(cellValue, @"\d{7,10}");

                            foreach (Match match in matches)
                            {
                                string posibleCedula = match.Value;

                                // Verificar si cumple con el formato de c�dula
                                if (EsCedulaValida(posibleCedula))
                                {
                                    // Normalizar la c�dula (eliminar ceros a la izquierda si es necesario)
                                    string cedulaNormalizada = posibleCedula;

                                    cedulasEncontradas.Add(cedulaNormalizada);
                                    cedulasConFila[cedulaNormalizada] = fila;
                                }
                            }

                            // Tambi�n buscar en celdas que contienen solo la c�dula
                            string soloNumeros = Regex.Replace(cellValue, @"[^\d]", "");
                            if (EsCedulaValida(soloNumeros))
                            {
                                cedulasEncontradas.Add(soloNumeros);
                                cedulasConFila[soloNumeros] = fila;
                            }
                        }
                    }

                    LogMessages += $"Se encontraron {cedulasEncontradas.Count} posibles c�dulas en la hoja {nombreHoja}\n";

                    // Procesar las c�dulas encontradas
                    foreach (string cedula in cedulasEncontradas)
                    {
                        // Verificar si existe en el archivo de referencia
                        if (usuariosReferencia.TryGetValue(cedula, out var usuario))
                        {
                            // Registrar que encontramos este usuario en esta hoja
                            if (!usuariosEncontrados.ContainsKey(cedula))
                                usuariosEncontrados[cedula] = new List<string>();

                            usuariosEncontrados[cedula].Add(nombreHoja);

                            // Si el usuario est� inactivo, registrarlo como error
                            if (!usuario.EstaActivo)
                            {
                                usuariosInactivosEncontrados.Add(cedula);

                                Resultados.Add(new ResultadosComparacion
                                {
                                    Nivel = nombreHoja,
                                    Cedula = cedula,
                                    Nombre = usuario.Nombre,
                                    Estado = "Error",
                                    Observacion = $"Usuario inactivo en planilla principal, pero presente en hoja de nivel (fila {cedulasConFila[cedula]})"
                                });
                            }
                        }
                        else
                        {
                            // Usuario en hoja de nivel pero no en planilla principal
                            Resultados.Add(new ResultadosComparacion
                            {
                                Nivel = nombreHoja,
                                Cedula = cedula,
                                Nombre = "Desconocido",
                                Estado = "Error",
                                Observacion = $"Usuario presente en hoja de nivel (fila {cedulasConFila[cedula]}) pero no existe en planilla principal"
                            });
                        }
                    }
                }
            }

            // Verificar usuarios activos y sus niveles
            foreach (var usuario in usuariosReferencia.Values)
            {
                if (!usuario.EstaActivo) continue; // Ignorar inactivos

                string cedula = usuario.Cedula;
                string nivelUsuario = usuario.Nivel.Trim();

                // Verificar si el nivel del usuario existe como hoja en planilla 2
                bool nivelExisteEnPlanilla2 = EsNivelDisponible(nivelUsuario, nivelesDisponibles, mapeoNivelesNormalizados);

                // Si el nivel no existe en planilla 2, ignorar este usuario
                if (!nivelExisteEnPlanilla2)
                {
                    LogMessages += $"Ignorando usuario {cedula} - {usuario.Nombre} con nivel '{nivelUsuario}' porque no existe como hoja en planilla 2\n";
                    continue;
                }

                // Si el usuario no fue encontrado en ninguna hoja, ignorarlo
                if (!usuariosEncontrados.ContainsKey(cedula))
                {
                    LogMessages += $"Ignorando usuario {cedula} - {usuario.Nombre} porque no se encontr� en ninguna hoja de nivel\n";
                    continue;
                }

                // Verificar si el usuario est� en el nivel correcto
                bool esCoordinador = nivelUsuario.Contains("/");

                if (esCoordinador)
                {
                    // Obtener todos los niveles posibles del coordinador
                    var nivelesPermitidos = nivelUsuario.Split('/')
                                                         .Select(n => n.Trim())
                                                         .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Verificar si el usuario est� en al menos uno de sus niveles permitidos
                    bool estaEnNivelPermitido = false;

                    foreach (var nivelEncontrado in usuariosEncontrados[cedula])
                    {
                        if (nivelesPermitidos.Any(np => EsNivelSimilar(nivelEncontrado, np, mapeoNivelesNormalizados)))
                        {
                            estaEnNivelPermitido = true;
                            break;
                        }
                    }

                    if (!estaEnNivelPermitido)
                    {
                        string nivelesEncontradosStr = string.Join(", ", usuariosEncontrados[cedula]);

                        Resultados.Add(new ResultadosComparacion
                        {
                            Nivel = nivelesEncontradosStr,
                            Cedula = cedula,
                            Nombre = usuario.Nombre,
                            Estado = "Error",
                            Observacion = $"Coordinador encontrado en nivel(es) incorrecto(s). Deber�a estar en alguno de: {nivelUsuario}"
                        });
                    }
                }
                else
                {
                    // Para usuarios con un solo nivel
                    bool nivelCorrecto = false;

                    foreach (var nivelEncontrado in usuariosEncontrados[cedula])
                    {
                        // Usar la funci�n de comparaci�n flexible
                        if (EsNivelSimilar(nivelEncontrado, nivelUsuario, mapeoNivelesNormalizados))
                        {
                            nivelCorrecto = true;
                            break;
                        }
                    }

                    if (!nivelCorrecto)
                    {
                        string nivelesEncontradosStr = string.Join(", ", usuariosEncontrados[cedula]);

                        Resultados.Add(new ResultadosComparacion
                        {
                            Nivel = nivelesEncontradosStr,
                            Cedula = cedula,
                            Nombre = usuario.Nombre,
                            Estado = "Error",
                            Observacion = $"Usuario est� en nivel incorrecto. Deber�a estar en: {nivelUsuario}"
                        });
                    }
                }
            }

            // Resumen de la comparaci�n
            int totalUsuariosActivos = usuariosReferencia.Values.Count(u => u.EstaActivo);
            int usuariosActivosConNivelDisponible = usuariosReferencia.Values.Count(u =>
                u.EstaActivo && EsNivelDisponible(u.Nivel, nivelesDisponibles, mapeoNivelesNormalizados)
            );

            int usuariosActivosEncontrados = usuariosEncontrados.Count(u =>
                usuariosReferencia.ContainsKey(u.Key) &&
                usuariosReferencia[u.Key].EstaActivo
            );

            LogMessages += $"Resumen: {totalUsuariosActivos} usuarios activos en planilla principal\n";
            LogMessages += $"De los cuales {usuariosActivosConNivelDisponible} tienen nivel disponible en planilla 2\n";
            LogMessages += $"Se encontraron {usuariosActivosEncontrados} usuarios activos en hojas de nivel\n";
            LogMessages += $"Total de inconsistencias: {Resultados.Count}\n";

            // Al final del m�todo CompararArchivos
            int usuariosEnNivelIncorrecto = usuariosEncontrados.Count(u =>
                usuariosReferencia.ContainsKey(u.Key) &&
                usuariosReferencia[u.Key].EstaActivo &&
                !usuariosEncontrados[u.Key].Any(nivel => EsNivelSimilar(nivel, usuariosReferencia[u.Key].Nivel, mapeoNivelesNormalizados))
            );

            LogMessages += $"Usuarios activos encontrados en nivel incorrecto: {usuariosEnNivelIncorrecto}\n";
            int usuariosNoEnPlanillaPrincipal = usuariosEncontrados.Count(u => !usuariosReferencia.ContainsKey(u.Key));
            LogMessages += $"Usuarios encontrados en hojas de nivel pero no en planilla principal: {usuariosNoEnPlanillaPrincipal}\n";

            LogMessages += "\n--- DIAGN�STICO DETALLADO ---\n";

            // Identificar usuarios que est�n en hojas de nivel pero cuyo nivel asignado no existe como hoja
            var usuariosEnHojaPeroSinNivelDisponible = usuariosEncontrados
                .Where(u =>
                    usuariosReferencia.ContainsKey(u.Key) &&
                    usuariosReferencia[u.Key].EstaActivo &&
                    !EsNivelDisponible(usuariosReferencia[u.Key].Nivel, nivelesDisponibles, mapeoNivelesNormalizados)
                )
                .ToList();

            LogMessages += $"Usuarios activos encontrados en hojas pero sin nivel disponible: {usuariosEnHojaPeroSinNivelDisponible.Count}\n";

            // Listar los primeros 10 como ejemplo
            if (usuariosEnHojaPeroSinNivelDisponible.Any())
            {
                LogMessages += "Ejemplos:\n";
                foreach (var kvp in usuariosEnHojaPeroSinNivelDisponible.Take(10))
                {
                    var cedula = kvp.Key;
                    var usuario = usuariosReferencia[cedula];
                    var nivelesEncontrados = string.Join(", ", kvp.Value);

                    LogMessages += $"- C�dula: {cedula}, Nombre: {usuario.Nombre}\n";
                    LogMessages += $"  Nivel asignado: '{usuario.Nivel}' (no disponible como hoja)\n";
                    LogMessages += $"  Encontrado en hojas: {nivelesEncontrados}\n";
                }
            }

            // Verificar si hay usuarios que aparecen en m�ltiples hojas
            var usuariosEnMultiplesHojas = usuariosEncontrados
                .Where(u => u.Value.Count > 1)
                .ToList();

            LogMessages += $"\nUsuarios que aparecen en m�ltiples hojas: {usuariosEnMultiplesHojas.Count}\n";

            // Listar los primeros 10 como ejemplo
            if (usuariosEnMultiplesHojas.Any())
            {
                LogMessages += "Ejemplos:\n";
                foreach (var kvp in usuariosEnMultiplesHojas.Take(10))
                {
                    var cedula = kvp.Key;
                    var nivelesEncontrados = string.Join(", ", kvp.Value);

                    string nombreUsuario = "Desconocido";
                    string nivelAsignado = "N/A";

                    if (usuariosReferencia.ContainsKey(cedula))
                    {
                        nombreUsuario = usuariosReferencia[cedula].Nombre;
                        nivelAsignado = usuariosReferencia[cedula].Nivel;
                    }

                    LogMessages += $"- C�dula: {cedula}, Nombre: {nombreUsuario}\n";
                    LogMessages += $"  Nivel asignado: '{nivelAsignado}'\n";
                    LogMessages += $"  Encontrado en hojas: {nivelesEncontrados}\n";
                }
            }

            // Verificar nombres de niveles similares pero no id�nticos
            LogMessages += "\nVerificaci�n de posibles problemas de formato en nombres de niveles:\n";

            var nivelesAsignados = usuariosReferencia.Values
                .Where(u => u.EstaActivo)
                .SelectMany(u => u.Nivel.Contains("/")
                    ? u.Nivel.Split('/').Select(n => n.Trim())
                    : new[] { u.Nivel.Trim() })
                .Distinct()
                .ToList();

            foreach (var nivelAsignado in nivelesAsignados)
            {
                if (!nivelesDisponibles.Contains(nivelAsignado))
                {
                    // Buscar nombres similares
                    var posiblesSimilares = nivelesDisponibles
                        .Where(n =>
                            n.Replace(" ", "").Equals(nivelAsignado.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
                            LevenshteinDistance(n, nivelAsignado) <= 2)
                        .ToList();

                    if (posiblesSimilares.Any())
                    {
                        LogMessages += $"- Nivel asignado '{nivelAsignado}' no encontrado como hoja, pero hay nombres similares: {string.Join(", ", posiblesSimilares)}\n";
                    }
                }
            }

            // Mostrar el mapeo de niveles utilizado
            LogMessages += "\n--- MAPEO DE NIVELES APLICADO ---\n";
            foreach (var kvp in mapeoNiveles)
            {
                LogMessages += $"- Nivel en planilla principal: '{kvp.Key}' ? Hoja en planilla 2: '{kvp.Value}'\n";
            }
        }

        // Funci�n para verificar si un nivel est� disponible, considerando similitudes
        private bool EsNivelDisponible(string nivelAsignado, HashSet<string> nivelesDisponibles, Dictionary<string, string> mapeoNivelesNormalizados)
        {
            // Verificaci�n exacta
            if (nivelesDisponibles.Contains(nivelAsignado))
            {
                return true;
            }

            // Verificar si hay un mapeo expl�cito
            if (mapeoNiveles.TryGetValue(nivelAsignado.Trim(), out string nivelMapeado))
            {
                if (nivelesDisponibles.Contains(nivelMapeado))
                {
                    return true;
                }
            }

            // Si el nivel contiene "/", verificar cada parte
            if (nivelAsignado.Contains("/"))
            {
                var partes = nivelAsignado.Split('/').Select(n => n.Trim());
                foreach (var parte in partes)
                {
                    if (nivelesDisponibles.Contains(parte) ||
                        (mapeoNiveles.TryGetValue(parte, out nivelMapeado) && nivelesDisponibles.Contains(nivelMapeado)))
                    {
                        return true;
                    }
                }
            }

            // Verificaci�n con normalizaci�n
            string nivelNormalizado = NormalizarNivel(nivelAsignado);

            if (mapeoNivelesNormalizados.ContainsKey(nivelNormalizado))
            {
                return true;
            }

            // Verificaci�n con distancia de Levenshtein
            foreach (var nivel in nivelesDisponibles)
            {
                if (LevenshteinDistance(NormalizarNivel(nivel), nivelNormalizado) <= 2)
                {
                    return true;
                }
            }

            return false;
        }

        // Funci�n para verificar si dos niveles son similares
        private bool EsNivelSimilar(string nivel1, string nivel2, Dictionary<string, string> mapeoNivelesNormalizados)
        {
            // Comparaci�n exacta
            if (string.Equals(nivel1, nivel2, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Verificar mapeo expl�cito
            if (mapeoNiveles.TryGetValue(nivel2, out string nivelMapeado) &&
                string.Equals(nivel1, nivelMapeado, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Verificar si nivel2 contiene m�ltiples opciones (separadas por /)
            if (nivel2.Contains("/"))
            {
                var opciones = nivel2.Split('/').Select(n => n.Trim());
                foreach (var opcion in opciones)
                {
                    if (string.Equals(nivel1, opcion, StringComparison.OrdinalIgnoreCase) ||
                        (mapeoNiveles.TryGetValue(opcion, out nivelMapeado) &&
                         string.Equals(nivel1, nivelMapeado, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            // Comparaci�n normalizada
            string n1 = NormalizarNivel(nivel1);
            string n2 = NormalizarNivel(nivel2);

            if (n1 == n2)
            {
                return true;
            }

            // Verificar similitud con distancia de Levenshtein
            return LevenshteinDistance(n1, n2) <= 2;
        }

        // Funci�n para calcular la distancia de Levenshtein (similitud entre strings)
        int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int[] v0 = new int[t.Length + 1];
            int[] v1 = new int[t.Length + 1];

            for (int i = 0; i < v0.Length; i++)
            {
                v0[i] = i;
            }

            for (int i = 0; i < s.Length; i++)
            {
                v1[0] = i + 1;

                for (int j = 0; j < t.Length; j++)
                {
                    int cost = (s[i] == t[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(Math.Min(v1[j] + 1, v0[j + 1] + 1), v0[j] + cost);
                }

                for (int j = 0; j < v0.Length; j++)
                {
                    v0[j] = v1[j];
                }
            }

            return v1[t.Length];
        }

        // Funci�n mejorada para validar c�dulas
        private bool EsCedulaValida(string cedula)
        {
            // Eliminar caracteres no num�ricos
            string soloNumeros = Regex.Replace(cedula, @"[^\d]", "");

            // Verificar que tenga entre 7 y 10 d�gitos (rango t�pico para c�dulas uruguayas)
            // La mayor�a de las c�dulas tienen 8 d�gitos, pero algunas pueden tener 7 o 9
            return !string.IsNullOrEmpty(soloNumeros) &&
                   soloNumeros.Length >= 7 &&
                   soloNumeros.Length <= 10;
        }

        // M�todo mejorado para normalizar los niveles para comparaci�n
        private string NormalizarNivel(string nivel)
        {
            if (string.IsNullOrEmpty(nivel))
            {
                return string.Empty;
            }

            // Eliminar espacios
            string normalizado = nivel.Trim();

            // Normalizar guiones (eliminar espacios alrededor de guiones)
            normalizado = Regex.Replace(normalizado, @"\s*[-_]\s*", "-");

            // Convertir guiones bajos a guiones normales
            normalizado = normalizado.Replace('_', '-');

            // Eliminar espacios y convertir a may�sculas para comparaci�n m�s flexible
            normalizado = normalizado.Replace(" ", "").ToUpper();

            return normalizado;
        }

        public class UsuarioReferencia
        {
            public string Cedula { get; set; }
            public string Nombre { get; set; }
            public bool EstaActivo { get; set; }
            public string Nivel { get; set; }
        }

        public class ResultadosComparacion
        {
            public string Nivel { get; set; }
            public string Cedula { get; set; }
            public string Nombre { get; set; }
            public string Estado { get; set; }
            public string Observacion { get; set; }
        }
    }
}
