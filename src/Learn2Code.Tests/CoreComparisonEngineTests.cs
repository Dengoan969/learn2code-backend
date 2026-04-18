using Learn2Code.Core.Enums;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Learn2Code.Services;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class CoreComparisonEngineTests
{
    private CoreComparisonEngine _engine;
    private MockLanguageAnalyzer _mockAnalyzer;

    [SetUp]
    public void SetUp()
    {
        _mockAnalyzer = new MockLanguageAnalyzer();
        _engine = new CoreComparisonEngine(_mockAnalyzer);
    }

    [Test]
    public void Compare_WhenStateMatchesExactly_ReturnsPassed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var catState = new CatState
        {
            GridX = 5,
            GridY = 3,
            Direction = 90.0,
            Visible = true,
            Costume = "default"
        };
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(catState);
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState
        {
            GridX = 5,
            GridY = 3,
            Direction = 90.0,
            Visible = true,
            Costume = "default"
        });
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.8,
            Level = CheckLevel.Normal
        };

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.True);
        Assert.That(result.Issues, Is.Empty);
    }

    [Test]
    public void Compare_WhenStateDoesNotMatch_ReturnsFailedWithIssue()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState
        {
            GridX = 5,
            GridY = 3,
            Direction = 90.0,
            Visible = true
        });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState
        {
            GridX = 10,  // Different position
            GridY = 3,
            Direction = 90.0,
            Visible = true
        });
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.8,
            Level = CheckLevel.Normal
        };

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
        Assert.That(result.Issues, Has.Count.EqualTo(1));
        Assert.That(result.Issues[0].Type, Is.EqualTo(IssueType.StateMismatch));
    }

    [Test]
    public void Compare_WhenTraceSimilarityHigh_ReturnsOptimal()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram
        {
            Elements = new List<CodeElement>
            {
                new CodeElement { Type = "move", Parameters = new Dictionary<string, object?> { { "cells", 2 } } },
                new CodeElement { Type = "turn", Parameters = new Dictionary<string, object?> { { "degrees", 90 }, { "direction", "right" } } }
            }
        };
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        
        var trace = new ExecutionTrace
        {
            Events = new List<ExecutionEvent>
            {
                new ExecutionEvent { Step = 1, EventType = "move", Details = new Dictionary<string, object> { { "cells", 2 } } },
                new ExecutionEvent { Step = 2, EventType = "turn", Details = new Dictionary<string, object> { { "degrees", 90 }, { "direction", "right" } } }
            }
        };
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = trace,
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.8,  // 80% similarity required
            Level = CheckLevel.Normal
        };

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.True);
        Assert.That(result.IsOptimal, Is.True);  // Trace similarity should be 1.0 (100%)
    }

    [Test]
    public void Compare_WhenTraceSimilarityLow_ReturnsNotOptimal()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram
        {
            Elements = new List<CodeElement>
            {
                new CodeElement { Type = "move", Parameters = new Dictionary<string, object?> { { "cells", 2 } } },
                new CodeElement { Type = "turn", Parameters = new Dictionary<string, object?> { { "degrees", 90 }, { "direction", "right" } } },
                new CodeElement { Type = "move", Parameters = new Dictionary<string, object?> { { "cells", 1 } } }
            }
        };
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        
        var trace = new ExecutionTrace
        {
            Events = new List<ExecutionEvent>
            {
                new ExecutionEvent { Step = 1, EventType = "move", Details = new Dictionary<string, object> { { "cells", 2 } } }
                // Missing turn and second move events
            }
        };
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = trace,
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.8,  // 80% similarity required
            Level = CheckLevel.Normal
        };

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.True);  // State matches
        Assert.That(result.IsOptimal, Is.False);  // Trace similarity is 1/3 ≈ 0.33 < 0.8
    }

    [Test]
    public void Compare_WhenExecutionFailed_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        var actualState = new SceneState();
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = false,
            Error = "Runtime error: division by zero"
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
        Assert.That(result.Issues, Has.Count.AtLeast(1));
        Assert.That(result.Issues[0].Type, Is.EqualTo(IssueType.SyntaxError));
    }

    [Test]
    public void Compare_WhenStateNull_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        
        var execution = new ExecutionResult
        {
            FinalState = null!,  // Null state
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
        Assert.That(result.Issues, Has.Count.AtLeast(1));
        Assert.That(result.Issues[0].Type, Is.EqualTo(IssueType.StateMismatch));
    }

    [Test]
    public void Compare_WhenExpectedStateNull_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, null!, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
        Assert.That(result.Issues, Has.Count.AtLeast(1));
        Assert.That(result.Issues[0].Type, Is.EqualTo(IssueType.StateMismatch));
    }

    [Test]
    public void Compare_WithAppleAndWallSprites_ComparesCorrectly()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        expectedState.Sprites.Add(new AppleState { GridX = 10, GridY = 7 });
        expectedState.Sprites.Add(new WallState { GridX = 15, GridY = 12 });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        actualState.Sprites.Add(new AppleState { GridX = 10, GridY = 7 });
        actualState.Sprites.Add(new WallState { GridX = 15, GridY = 12 });
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.True);
    }

    [Test]
    public void Compare_WithMissingAppleSprite_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        expectedState.Sprites.Add(new AppleState { GridX = 10, GridY = 7 });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        // Missing apple sprite
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
    }

    [Test]
    public void Compare_WithExtraAppleSprite_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        expectedState.Sprites.Add(new AppleState { GridX = 10, GridY = 7 });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3 });
        actualState.Sprites.Add(new AppleState { GridX = 10, GridY = 7 });
        actualState.Sprites.Add(new AppleState { GridX = 12, GridY = 8 });  // Extra apple
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
    }

    [Test]
    public void Compare_WithCatDirectionDifference_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        expectedState.Sprites.Add(new CatState { GridX = 5, GridY = 3, Direction = 90.0 });
        
        var actualState = new SceneState();
        actualState.Sprites.Add(new CatState { GridX = 5, GridY = 3, Direction = 180.0 });  // Different direction
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
    }

    [Test]
    public void Compare_WithCatSaidTexts_ComparesCorrectly()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        var expectedCat = new CatState { GridX = 5, GridY = 3 };
        expectedCat.SaidTexts["Hello"] = 1;
        expectedCat.SaidTexts["World"] = 2;
        expectedState.Sprites.Add(expectedCat);
        
        var actualState = new SceneState();
        var actualCat = new CatState { GridX = 5, GridY = 3 };
        actualCat.SaidTexts["Hello"] = 1;
        actualCat.SaidTexts["World"] = 2;
        actualState.Sprites.Add(actualCat);
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.True);
    }

    [Test]
    public void Compare_WithDifferentCatSaidTexts_ReturnsFailed()
    {
        // Arrange
        var studentProgram = new NormalizedProgram();
        var referenceProgram = new NormalizedProgram();
        
        var expectedState = new SceneState();
        var expectedCat = new CatState { GridX = 5, GridY = 3 };
        expectedCat.SaidTexts["Hello"] = 1;
        expectedCat.SaidTexts["World"] = 2;
        expectedState.Sprites.Add(expectedCat);
        
        var actualState = new SceneState();
        var actualCat = new CatState { GridX = 5, GridY = 3 };
        actualCat.SaidTexts["Hello"] = 1;
        actualCat.SaidTexts["Test"] = 1;  // Different text
        actualState.Sprites.Add(actualCat);
        
        var execution = new ExecutionResult
        {
            FinalState = actualState,
            Trace = new ExecutionTrace(),
            Success = true,
            Error = null
        };
        
        var config = new TaskConfig();

        // Act
        var result = _engine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False);
    }

    private class MockLanguageAnalyzer : ILanguageAnalyzer
    {
        public bool Supports(string languageId)
        {
            return languageId == "python" || languageId == "blockly";
        }

        public Task<NormalizedProgram> ExtractAsync(string code)
        {
            return Task.FromResult(new NormalizedProgram());
        }
    }
}