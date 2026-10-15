using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Filter;
using ApiQueryOptions.Options;

namespace ApiQueryOptions.Tests;

[TestClass]
public class FilterQueryOptionConcurrencyTests
{
    private const int Iterations = 200;
    private const int Readers = 16;

    private static T[] ReadConcurrently<T>(Func<T> read)
    {
        using var barrier = new Barrier(Readers);
        Task<T>[] tasks = Enumerable.Range(0, Readers)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                barrier.SignalAndWait();
                return read();
            }, TaskCreationOptions.LongRunning))
            .ToArray();

        return Task.WhenAll(tasks).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void FilterClause_ConcurrentFirstAccess_EveryReaderGetsTheSameParsedClause()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var option = new FilterQueryOption("Name eq 'Rex' and (Id gt 1 or contains(Name, 'e'))");

            FilterClause[] results = ReadConcurrently(() => option.FilterClause);

            results.Should().AllSatisfy(r => r.Should().NotBeNull().And.BeSameAs(results[0]));
        }
    }

    [TestMethod]
    public void FilterClause_ConcurrentFirstAccess_InvalidFilter_EveryReaderGetsTheParseError()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var option = new FilterQueryOption("Name eq");

            FilterParseException?[] errors = ReadConcurrently<FilterParseException?>(() =>
            {
                try
                {
                    _ = option.FilterClause;
                    return null;
                }
                catch (FilterParseException ex)
                {
                    return ex;
                }
            });

            errors.Should().AllSatisfy(e => e.Should().NotBeNull());
        }
    }

    [TestMethod]
    public void FilterClause_RepeatedAccess_ParsesOnce()
    {
        var option = new FilterQueryOption("Id eq 1");

        option.FilterClause.Should().BeSameAs(option.FilterClause);
    }
}
