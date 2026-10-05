namespace WiiGC.Core.Models;

internal sealed record WiiVerifyResult(long Groups, long Mismatches, bool H3Matches, bool TmdHashMatches);