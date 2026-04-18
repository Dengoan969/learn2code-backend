namespace Learn2Code.Core.Enums;

public enum CheckLevel
{
    Strict, // Строгая проверка (точное совпадение)
    Normal, // Стандартная проверка (допустимы небольшие отклонения)
    Lenient // Мягкая проверка (только критерий прохождения)
}