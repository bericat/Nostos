using Nostos.Sim;

namespace Nostos
{
    /// <summary>Una frase de una escena: quién habla y qué dice.</summary>
    public struct Linea
    {
        public readonly string Quien;
        public readonly string Texto;

        public Linea(string quien, string texto)
        {
            Quien = quien;
            Texto = texto;
        }
    }

    /// <summary>
    /// Todo el texto narrativo de Nostos en un solo sitio: la introducción, las escenas que
    /// saltan al avanzar la partida, lo que dice cada colono según la confianza y el final.
    /// </summary>
    public static class Textos
    {
        public const string Nicias = "Nicias";

        public static readonly string[] Intro =
        {
            "Calcis no pierde naves.\n\nEso decía el manual, en la primera página, con letras doradas.",
            "La <i>Anábasis</i> perdió el motor a media órbita de Lacio, un mundo de paso que en los mapas de Calcis\nsolo aparece como una cifra: lo que debe de tributo cada estación.",
            "Nicias, ingeniero de segunda, salió del casco por su propio pie.\nFue el único.",
            "Al otro lado del valle hay humo. Una hoguera, tejados de madera, gente que mira hacia aquí sin acercarse.\n\nNova Lavinia. Colonos. Los que pagan la cifra.",
            "La nave puede volver a volar. No sola, y no con lo que queda dentro.\n\n<b>Nostos</b>: el regreso."
        };

        public const string PrimerObjetivo = "Tala árboles (E) y marca un refugio en el modo constructor (Tab). La noche cae a las 20:00.";

        // ---- Escenas ----

        public static Linea[] Escena(string id)
        {
            switch (id)
            {
                case "refugio":
                    return new[]
                    {
                        new Linea(Nicias, "Cuatro paredes y una cama. En Calcis esto no llegaría a trastero."),
                        new Linea(Nicias, "Aquí es lo primero que he hecho con las manos en diez años."),
                        new Linea("Marco", "¿Lo has hecho tú solo? Mi abuelo dice que los de Calcis no sabéis ni atar un nudo.")
                    };
                case "dia2":
                    return new[]
                    {
                        new Linea("Septimio", "Has pasado la noche. Bien. Eso ya es más de lo que esperaba el consejo."),
                        new Linea("Septimio", "Te lo digo una vez: aquí nadie trabaja para ti porque lo mande un sello. Si quieres brazos, gánatelos."),
                        new Linea(Nicias, "Solo quiero arreglar la nave e irme."),
                        new Linea("Septimio", "Todos los de Calcis solo quieren irse. Y siempre se llevan algo.")
                    };
                case "acuerdo":
                    return new[]
                    {
                        new Linea("Livia", "Así que ya tienes a alguien trabajando contigo. Con un acuerdo, de palabra."),
                        new Linea("Livia", "Aquí la palabra se apunta. Jornada, raciones, reparto, herramientas, y quién decide. Si aprietas, rinden más y te aguantan menos."),
                        new Linea(Nicias, "En Calcis eso se llama contrato."),
                        new Linea("Livia", "En Calcis el contrato lo escribe uno solo.")
                    };
                case "arconte":
                    return new[]
                    {
                        new Linea("Arconte", "Ingeniero Nicias. Calcis le daba por muerto. Me alegra corregir el registro."),
                        new Linea("Arconte", "Vengo por la cuota de la estación. Usted conoce el procedimiento: lo que no se entrega, se retira."),
                        new Linea(Nicias, "Estas personas apenas tienen para ellas."),
                        new Linea("Arconte", "Estas personas tienen un valle entero. Usted, en cambio, tiene un billete de vuelta que depende de mi informe. Estoy en la plaza hasta que caiga el día.")
                    };
                case "confianza":
                    return new[]
                    {
                        new Linea("Marco", "¡Nicias! La gente ya no baja la voz cuando pasas. Lo he comprobado."),
                        new Linea("Livia", "No te lo creas mucho. Pero es verdad. Empiezan a pensar que la nave no es solo tuya."),
                        new Linea(Nicias, "No lo es. Sin ellos no despega.")
                    };
                case "astillero":
                    return new[]
                    {
                        new Linea("Livia", "Un astillero. En Nova Lavinia. Mi abuela construía barcas de río y decía que eso era lo más grande que haríamos nunca."),
                        new Linea(Nicias, "La grúa es vuestra. Cuando me vaya, se queda."),
                        new Linea("Livia", "Eso ya lo había decidido yo.")
                    };
                case "teleferico":
                    return new[]
                    {
                        new Linea("Marco", "¡Se mueve solo! ¡La vagoneta se mueve sola!"),
                        new Linea("Cayo", "Treinta años cargando piedra a la espalda. Y viene uno de Calcis y cuelga un cable."),
                        new Linea("Cayo", "No te voy a dar las gracias. Pero mañana la engraso yo.")
                    };
                case "modulo1":
                    return new[]
                    {
                        new Linea(Nicias, "El casco vuelve a cerrar. Chapa de su fundición, tablones de su aserradero."),
                        new Linea("Cayo", "Esa chapa la he sacado yo de la veta. Que conste en algún sitio, aunque sea en tu cabeza.")
                    };
                case "modulo2":
                    return new[]
                    {
                        new Linea("Fabia", "¿Agua y raciones para el viaje? ¿Cuánto dura eso de volver?"),
                        new Linea(Nicias, "Cuarenta días, si el motor aguanta."),
                        new Linea("Fabia", "Cuarenta días solo. Yo no aguanto ni una tarde sin que alguien me lleve la contraria.")
                    };
                case "modulo3":
                    return new[]
                    {
                        new Linea("Livia", "He mirado tu motor. Las bobinas de Calcis son más finas, pero las nuestras se pueden arreglar con un martillo."),
                        new Linea(Nicias, "En Calcis, si algo se rompe, se pide otro."),
                        new Linea("Livia", "Ya. ¿Y a quién se lo pedís?")
                    };
                case "modulo4":
                    return new[]
                    {
                        new Linea("Marco", "¿Con esas lentes se ve Calcis? ¿Se ve tu casa?"),
                        new Linea(Nicias, "Se ve una luz. Muy pequeña."),
                        new Linea("Marco", "Pues desde allí nosotros ni eso. Qué raro que nos encontréis siempre para cobrar.")
                    };
                case "modulo5":
                    return new[]
                    {
                        new Linea("Septimio", "El consejo ha votado. Los núcleos son tuyos, y quien los carga contigo va porque quiere."),
                        new Linea("Septimio", "No lo hacemos por Calcis. Lo hacemos porque uno de los nuestros se va a casa."),
                        new Linea("Livia", "La nave está lista, Nicias. Cuando quieras. No hace falta que sea hoy."),
                        new Linea(Nicias, "...")
                    };
                case "vuelo":
                    return new[]
                    {
                        new Linea("Marco", "¡Ha volado! ¡Sin nadie dentro! ¡Y ha vuelto a bajar!"),
                        new Linea("Cayo", "Siete metros. Mi cabra salta más."),
                        new Linea("Livia", "Ha bajado entera, que es lo que importa. El motor suena a nuestro, Nicias."),
                        new Linea("Septimio", "Ahora ya no es un trasto en el prado. Es una nave. Y se va a ir.")
                    };
                default:
                    return null;
            }
        }

        // ---- Colonos ----

        public static string Saludo(Colono c)
        {
            int nivel = (int)c.Nivel;
            switch (c.Nombre)
            {
                case "Livia":
                    return Elegir(nivel,
                        "«Ingeniero de Calcis. Yo también soy ingeniera, aunque allí no me dejarían ni barrer el taller. ¿Qué quieres?»",
                        "«He visto cómo sueldas. No está mal, para alguien que aprendió con manuales. Dime.»",
                        "«Nicias. Pasa, que tengo un plano que enseñarte. Y sí, cuenta conmigo.»");
                case "Septimio":
                    return Elegir(nivel,
                        "«He visto llegar a tres como tú. Los tres prometieron algo. Habla, pero no prometas.»",
                        "«El consejo te escucha, que no es poco. Di lo que necesitas y lo que das a cambio.»",
                        "«Siéntate, hijo. A mi edad uno ya distingue al que cobra del que construye.»");
                case "Marco":
                    return Elegir(nivel,
                        "«Mi madre dice que no hable contigo. ¿Es verdad que en Calcis las casas flotan?»",
                        "«¡Nicias! ¿Puedo ver la nave por dentro? Solo mirar. Bueno, y tocar un poco.»",
                        "«Cuando sea mayor voy a hacer una nave. Pero la mía va a volver.»");
                case "Fabia":
                    return Elegir(nivel,
                        "«Si vienes a decirme cómo se tala un árbol, ahórratelo. ¿Qué quieres?»",
                        "«Trabajas, eso lo reconozco. Pocas manos de Calcis he visto con callos. Dime.»",
                        "«Hombre, el náufrago. Ven, que hoy la leña sale sola. ¿Qué necesitas?»");
                case "Cayo":
                    return Elegir(nivel,
                        "«La piedra no pregunta de dónde eres. Yo sí. Y no me gusta la respuesta.»",
                        "«Pagaste cuando tocaba pagar. Eso en la cantera cuenta. Habla.»",
                        "«Nicias. Si hay que mover una montaña para que vueles, dime por qué lado empiezo.»");
                default:
                    return Elegir(nivel,
                        "«Otro de Calcis. Aquí cada cual se gana el pan. Si quieres algo de mí, empieza por traer algo.»",
                        "«No eres como los arcontes, eso ya lo he visto. Dime qué necesitas y vemos en qué condiciones.»",
                        "«Cuenta conmigo, Nicias. Esa nave también es un poco nuestra.»");
            }
        }

        /// <summary>Lo que cuenta un colono cuando Nicias se para a charlar. Cambia con la confianza.</summary>
        public static string Charla(Colono c, int vez)
        {
            string[] frases = FrasesDe(c);
            return frases[((vez % frases.Length) + frases.Length) % frases.Length];
        }

        static string[] FrasesDe(Colono c)
        {
            bool cerca = c.Nivel != NivelConfianza.Baja;
            switch (c.Nombre)
            {
                case "Livia":
                    return cerca
                        ? new[]
                        {
                            "«Vinimos en una nave de carga, cuatrocientos en una bodega. Yo tenía nueve años y desmonté el filtro de aire para ver cómo era. Casi nos ahogo a todos.»",
                            "«Todo lo que sé lo aprendí arreglando lo que Calcis tira. Vuestra basura es nuestra escuela.»",
                            "«A veces pienso qué habría hecho yo con tus talleres. Luego miro la fundición y pienso qué harías tú sin los nuestros.»"
                        }
                        : new[]
                        {
                            "«Tengo trabajo. Si quieres conversación, trae antes lo que te pedí.»",
                            "«No me mires las herramientas. Son viejas, pero son mías.»"
                        };
                case "Septimio":
                    return cerca
                        ? new[]
                        {
                            "«Nova Lavinia. Le pusimos el nombre de una ciudad que alguien fundó después de perder la suya. Nadie funda nada por gusto.»",
                            "«El primer invierno enterramos a once. El arconte vino igual, con su lista. Apuntó once menos y no bajó la cuota.»",
                            "«Te vas a ir, y haces bien. Solo te pido que cuando estés allí arriba te acuerdes de que aquí abajo había caras.»"
                        }
                        : new[]
                        {
                            "«El consejo no tiene nada que decirte todavía.»",
                            "«Los viejos hablamos poco con los que están de paso.»"
                        };
                case "Marco":
                    return new[]
                    {
                        "«¿Sabes que la hoguera no se ha apagado nunca? Bueno, una vez. Pero fue culpa de Cayo, no mía.»",
                        "«Livia dice que tu nave tiene un núcleo que brilla. ¿Me lo enseñas? Yo te enseño dónde hay ranas.»",
                        "«Si te vas, ¿quién me va a contar cosas de las estrellas? Fabia solo sabe de árboles.»"
                    };
                case "Fabia":
                    return cerca
                        ? new[]
                        {
                            "«Cada árbol que tiro, planto dos. No por bondad: es que pienso seguir aquí.»",
                            "«Mi padre talaba para Calcis. Le pagaban con vales. Yo los uso para encender la lumbre.»",
                            "«Tu nave me da igual, te lo digo de frente. Pero tú no. Así que la acabamos.»"
                        }
                        : new[]
                        {
                            "«El bosque no es tuyo. Coge lo que necesites y nada más.»",
                            "«Menos charla. Hay luz hasta las seis.»"
                        };
                case "Cayo":
                    return cerca
                        ? new[]
                        {
                            "«La veta buena está al norte. El arconte no lo sabe, y así se va a quedar.»",
                            "«Perdí a mi hermano en la cantera, sacando la cuota de un año malo. Por eso al principio no te miraba.»",
                            "«Cuando despegues, pasa bajo sobre la cantera. Quiero ver si la chapa aguanta.»"
                        }
                        : new[]
                        {
                            "«No tengo nada que contarle a Calcis.»",
                            "«Aparta, que esto pesa.»"
                        };
                default:
                    return new[] { "«Aquí seguimos.»" };
            }
        }

        static string Elegir(int nivel, string baja, string media, string alta)
        {
            return nivel <= 0 ? baja : nivel == 1 ? media : alta;
        }

        // ---- Final ----

        public static string Final(int dia, float confianza)
        {
            string despedida = confianza >= GestorColonos.UmbralAlta
                ? "Toda Nova Lavinia ha subido a la loma. Nadie ha traído la lista de la cuota; alguien ha traído pan para el viaje."
                : "En la loma hay menos gente de la que esperaba. La suficiente para que duela.";
            return "<b>Nostos</b>\n\n"
                   + "La nave se levanta sobre el valle. Abajo quedan el aserradero, la fundición, la hoguera,\n"
                   + "Livia con la mano en alto, Marco corriendo detrás de la sombra del casco.\n\n"
                   + despedida + "\n\n"
                   + "Nicias vuelve a Calcis. Lleva en el informe una palabra que el formulario no admite: <i>nombres</i>.\n"
                   + "Volver no es ganar.\n\n"
                   + "Día " + dia + " · confianza de Nova Lavinia: " + UnityEngine.Mathf.RoundToInt(confianza)
                   + "\n\nIntro / A: volver al título";
        }
    }
}
