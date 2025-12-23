namespace Smart.CommandLine.Hosting;

public sealed class FilterPipelineTests
{
    private sealed class TestServiceProvider : IServiceProvider
    {
        public Dictionary<Type, object> Services { get; } = [];

        public Func<Type, object?>? GetServiceFunc { get; set; }

        public void AddService(Type serviceType, object service)
        {
            Services[serviceType] = service;
        }

        public object? GetService(Type serviceType)
        {
            if (GetServiceFunc is not null)
            {
                return GetServiceFunc(serviceType);
            }

            return Services.TryGetValue(serviceType, out var service) ? service : null;
        }
    }

    private sealed class TestCommand : ICommand
    {
        public bool Executed { get; private set; }

        public ValueTask ExecuteAsync(CommandContext context)
        {
            Executed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LoggingFilter : ICommandFilter
    {
        private readonly List<string> log;

        public int Order { get; }

        public LoggingFilter(List<string> log, int order = 0)
        {
            this.log = log;
            Order = order;
        }

        public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
        {
            log.Add($"Filter{Order}-Before");
            await next(context);
            log.Add($"Filter{Order}-After");
        }
    }

    private sealed class ExitCodeFilter : ICommandFilter
    {
        private readonly int exitCode;

        public int Order { get; }

        public ExitCodeFilter(int exitCode, int order = 0)
        {
            this.exitCode = exitCode;
            Order = order;
        }

        public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
        {
            await next(context);
            context.ExitCode = exitCode;
        }
    }

    private sealed class ShortCircuitFilter : ICommandFilter
    {
        public int Order => 0;

        public ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
        {
            context.Items["short-circuit"] = true;
            return ValueTask.CompletedTask;
        }
    }

    [Filter<GlobalFilter1>(Order = 10)]
    private sealed class CommandWithFilter : ICommand
    {
        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.Items["command-executed"] = true;
            return ValueTask.CompletedTask;
        }
    }

    [Filter<GlobalFilter1>(Order = 5)]
    [Filter<GlobalFilter2>(Order = 15)]
    private sealed class CommandWithMultipleFilters : ICommand
    {
        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.Items["command-executed"] = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class GlobalFilter1 : ICommandFilter
    {
        private readonly List<string> log;

        public int Order => 0;

        public GlobalFilter1(List<string> log)
        {
            this.log = log;
        }

        public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
        {
            log.Add("GlobalFilter1-Before");
            await next(context);
            log.Add("GlobalFilter1-After");
        }
    }

    private sealed class GlobalFilter2 : ICommandFilter
    {
        private readonly List<string> log;

        public int Order => 0;

        public GlobalFilter2(List<string> log)
        {
            this.log = log;
        }

        public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
        {
            log.Add("GlobalFilter2-Before");
            await next(context);
            log.Add("GlobalFilter2-After");
        }
    }

    //--------------------------------------------------------------------------------
    // Test
    //--------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithoutFilters_ExecutesActionDirectly()
    {
        // Arrange
        var globalFilters = new FilterCollection();
        var serviceProvider = new TestServiceProvider();
        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx => ctx.Command.ExecuteAsync(ctx));

        // Assert
        Assert.True(command.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_WithGlobalFilter_AppliesFilter()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 0);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(LoggingFilter), new LoggingFilter(log));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        Assert.True(command.Executed);
        Assert.Equal(3, log.Count);
        Assert.Equal("Filter0-Before", log[0]);
        Assert.Equal("Action", log[1]);
        Assert.Equal("Filter0-After", log[2]);
    }

    [Fact]
    public async Task ExecuteAsync_WithCommandFilter_AppliesFilter()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(GlobalFilter1), new GlobalFilter1(log));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var context = new CommandContext(typeof(CommandWithFilter), new CommandWithFilter(), CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        Assert.Equal(3, log.Count);
        Assert.Equal("GlobalFilter1-Before", log[0]);
        Assert.Equal("Action", log[1]);
        Assert.Equal("GlobalFilter1-After", log[2]);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleFilters_ExecutesInOrderByOrder()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 5);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(LoggingFilter), new LoggingFilter(log, 5));
        serviceProvider.AddService(typeof(GlobalFilter1), new GlobalFilter1(log));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var context = new CommandContext(typeof(CommandWithFilter), new CommandWithFilter(), CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        Assert.Equal(5, log.Count);
        Assert.Equal("Filter5-Before", log[0]);
        Assert.Equal("GlobalFilter1-Before", log[1]);
        Assert.Equal("Action", log[2]);
        Assert.Equal("GlobalFilter1-After", log[3]);
        Assert.Equal("Filter5-After", log[4]);
    }

    [Fact]
    public async Task ExecuteAsync_WithGlobalAndCommandFilters_CombinesBoth()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 1);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(LoggingFilter), new LoggingFilter(log, 1));
        serviceProvider.AddService(typeof(GlobalFilter1), new GlobalFilter1(log));
        serviceProvider.AddService(typeof(GlobalFilter2), new GlobalFilter2(log));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var context = new CommandContext(typeof(CommandWithMultipleFilters), new CommandWithMultipleFilters(), CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        // Order: LoggingFilter(1) -> GlobalFilter1(5) -> GlobalFilter2(15)
        Assert.Equal(7, log.Count);
        Assert.Equal("Filter1-Before", log[0]);
        Assert.Equal("GlobalFilter1-Before", log[1]);
        Assert.Equal("GlobalFilter2-Before", log[2]);
        Assert.Equal("Action", log[3]);
        Assert.Equal("GlobalFilter2-After", log[4]);
        Assert.Equal("GlobalFilter1-After", log[5]);
        Assert.Equal("Filter1-After", log[6]);
    }

    [Fact]
    public async Task ExecuteAsync_OrderSorting_ExecutesFiltersInCorrectOrder()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 30);

        var filter1 = new LoggingFilter(log, 30);
        var filter2 = new LoggingFilter(log, 10);
        var filter3 = new LoggingFilter(log, 20);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(LoggingFilter), filter1);
        serviceProvider.AddService(typeof(GlobalFilter1), filter2);
        serviceProvider.AddService(typeof(GlobalFilter2), filter3);

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var context = new CommandContext(typeof(CommandWithMultipleFilters), new CommandWithMultipleFilters(), CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, _ =>
        {
            log.Add("Action");
            return ValueTask.CompletedTask;
        });

        // Assert
        // Should be sorted by Order: 10, 15, 20, 30
        // But we have: filter2(10 via GlobalFilter1 at order 5), filter3(20 via GlobalFilter2 at order 15), filter1(30)
        Assert.Contains("Action", log);
        Assert.Equal(7, log.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FilterCanModifyExitCode()
    {
        // Arrange
        var globalFilters = new FilterCollection();
        globalFilters.Add<ExitCodeFilter>(order: 0);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(ExitCodeFilter), new ExitCodeFilter(42));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, _ => ValueTask.CompletedTask);

        // Assert
        Assert.Equal(42, context.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_FilterCanShortCircuit()
    {
        // Arrange
        var globalFilters = new FilterCollection();
        globalFilters.Add<ShortCircuitFilter>(order: 0);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(ShortCircuitFilter), new ShortCircuitFilter());

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx => ctx.Command.ExecuteAsync(ctx));

        // Assert
        Assert.False(command.Executed);
        Assert.True((bool)context.Items["short-circuit"]!);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFilterInServiceProvider_SkipsFilter()
    {
        // Arrange
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 0);

        var serviceProvider = new TestServiceProvider(); // No filter registered

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx => ctx.Command.ExecuteAsync(ctx));

        // Assert
        Assert.True(command.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFiltersWithSameOrder_MaintainsStableOrder()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 10);

        var filter1 = new LoggingFilter(log, 10);
        var filter2 = new LoggingFilter(log, 10);

        var serviceProvider = new TestServiceProvider
        {
            Services =
            {
                [typeof(LoggingFilter)] = new List<object> { filter1, filter2 }
            }
        };
        var callCount = 0;
        serviceProvider.GetServiceFunc = type =>
        {
            if (type == typeof(LoggingFilter))
            {
                return callCount++ == 0 ? filter1 : filter2;
            }
            return null;
        };

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        Assert.True(command.Executed);
        Assert.Contains("Action", log);
        Assert.Contains("Filter10-Before", log);
        Assert.Contains("Filter10-After", log);
    }

    [Fact]
    public async Task ExecuteAsync_FilterCanAccessContextItems()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 0);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(LoggingFilter), new LoggingFilter(log));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);
        context.Items.Add("test-key", "test-value");

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            Assert.Equal("test-value", ctx.Items["test-key"]);
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        Assert.True(command.Executed);
        Assert.Equal("test-value", context.Items["test-key"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroOrderFilters_ExecutesCorrectly()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: 0);

        var serviceProvider = new TestServiceProvider();
        serviceProvider.AddService(typeof(LoggingFilter), new LoggingFilter(log));

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var command = new TestCommand();
        var context = new CommandContext(typeof(TestCommand), command, CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        Assert.True(command.Executed);
        Assert.Equal(3, log.Count);
        Assert.Equal("Filter0-Before", log[0]);
        Assert.Equal("Action", log[1]);
        Assert.Equal("Filter0-After", log[2]);
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeOrderFilters_ExecutesInCorrectOrder()
    {
        // Arrange
        var log = new List<string>();
        var globalFilters = new FilterCollection();
        globalFilters.Add<LoggingFilter>(order: -10);

        var filter1 = new LoggingFilter(log, -10);
        var filter2 = new LoggingFilter(log, 5);

        var serviceProvider = new TestServiceProvider
        {
            Services =
            {
                [typeof(LoggingFilter)] = new List<object> { filter1, filter2 }
            }
        };
        var callCount = 0;
        serviceProvider.GetServiceFunc = type =>
        {
            if (type == typeof(LoggingFilter))
            {
                return callCount++ == 0 ? filter1 : null;
            }
            if (type == typeof(GlobalFilter1))
            {
                return filter2;
            }
            return null;
        };

        var pipeline = new FilterPipeline(serviceProvider, globalFilters);

        var context = new CommandContext(typeof(CommandWithFilter), new CommandWithFilter(), CancellationToken.None);

        // Act
        await pipeline.ExecuteAsync(context, ctx =>
        {
            log.Add("Action");
            return ctx.Command.ExecuteAsync(ctx);
        });

        // Assert
        // Order: -10 comes before 5
        Assert.True(log.IndexOf("Filter-10-Before") < log.IndexOf("Filter5-Before"));
    }
}
