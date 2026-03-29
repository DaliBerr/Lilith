namespace Vocalith.Scribe
{
    public static class ScribeBootstrap
    {
        private static bool _initialized;

        /// <summary>
        /// 注册 Scribe 自带的基础 codec；重复调用安全。
        /// </summary>
        /// <param name="none">无</param>
        /// <returns>无</returns>
        public static void InitializeDefaults()
        {
            if (_initialized)
            {
                return;
            }

            CodecRegistry.Register(new BoolCodec());
            CodecRegistry.Register(new IntCodec());
            CodecRegistry.Register(new FloatCodec());
            CodecRegistry.Register(new StringCodec());
            CodecRegistry.Register(new LongCodec());

            _initialized = true;
        }
    }
}
