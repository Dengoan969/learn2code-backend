namespace Learn2Code.Core.Enums;

public enum IssueType
{
    StateMismatch, // Несоответствие состояния сцены
    TraceMismatch, // Несоответствие трассы событий
    RedundantCode, // Лишний код
    MissingElement, // Отсутствие необходимого элемента
    SemanticMismatch, // Семантическое несоответствие
    ParameterMismatch, // Несоответствие параметров
    SyntaxError // Ошибка синтаксиса
}