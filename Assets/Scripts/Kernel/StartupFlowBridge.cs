namespace Kernel
{
    public static class StartupFlowBridge
    {
        private static object owner;
        private static System.Func<bool> bootCompletedProvider;
        private static System.Func<bool> startGameRequester;
        private static System.Func<bool> enterMainSceneRequester;

        public static bool HasStartup => owner != null;

        public static bool IsBootCompleted => bootCompletedProvider?.Invoke() == true;

        public static void Register(
            object startupOwner,
            System.Func<bool> isBootCompleted,
            System.Func<bool> requestStartGame,
            System.Func<bool> requestEnterMainScene)
        {
            owner = startupOwner;
            bootCompletedProvider = isBootCompleted;
            startGameRequester = requestStartGame;
            enterMainSceneRequester = requestEnterMainScene;
        }

        public static void Unregister(object startupOwner)
        {
            if (owner != startupOwner)
            {
                return;
            }

            owner = null;
            bootCompletedProvider = null;
            startGameRequester = null;
            enterMainSceneRequester = null;
        }

        public static bool RequestStartGame()
        {
            return startGameRequester?.Invoke() == true;
        }

        public static bool RequestEnterMainScene()
        {
            return enterMainSceneRequester?.Invoke() == true;
        }
    }
}
