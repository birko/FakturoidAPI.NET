using Xunit;

// Live tests share one Fakturoid account; run test classes one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
