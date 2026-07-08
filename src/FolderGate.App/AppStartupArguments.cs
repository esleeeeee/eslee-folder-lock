using FolderGate.Core.Localization;

namespace FolderGate.App;

public sealed record AppStartupArguments(string? UnlockPath, string? RootPath, bool ResumeTemporaryUnlocks)
{
    public static AppStartupArguments Parse(IReadOnlyList<string> args)
    {
        string? unlockPath = null;
        string? rootPath = null;
        bool resumeTemporaryUnlocks = false;

        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (string.Equals(token, "--unlock-path", StringComparison.OrdinalIgnoreCase))
            {
                unlockPath = ReadRequiredValue(args, ref i, token);
                continue;
            }

            if (string.Equals(token, "--unlock", StringComparison.OrdinalIgnoreCase))
            {
                unlockPath = ReadRequiredValue(args, ref i, token);
                continue;
            }

            if (string.Equals(token, "--root", StringComparison.OrdinalIgnoreCase))
            {
                rootPath = ReadRequiredValue(args, ref i, token);
                continue;
            }

            if (string.Equals(token, "--resume-temporary-unlocks", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "--relock-expired", StringComparison.OrdinalIgnoreCase))
            {
                resumeTemporaryUnlocks = true;
                continue;
            }

            throw new ArgumentException(AppText.UnknownStartupArgument(token));
        }

        return new AppStartupArguments(unlockPath, rootPath, resumeTemporaryUnlocks);
    }

    private static string ReadRequiredValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException(AppText.OptionRequiresValue(optionName));
        }

        index++;
        return args[index];
    }
}
