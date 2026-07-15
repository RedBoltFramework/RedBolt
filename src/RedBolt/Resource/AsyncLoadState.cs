// C++: enum AsyncLoadState in Resource.h:69.
// No non-allowed divergences found. Prefix ASYNC_ → PascalCase is allowed (enum naming convention).
namespace RedBolt;

public enum AsyncLoadState
{
    Done,
    Queued,
    Loading,
    Success,
    Fail
}
