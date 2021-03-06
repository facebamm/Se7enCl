﻿using Se7en.OpenCl.Api.Enum;
using Se7en.OpenCl.Api.Native;
using System;

namespace Se7en.OpenCl
{
    public readonly unsafe struct OpenClCompiler : IDisposable
    {
        public readonly static Platform[] Platforms;
        public readonly static uint PlatformCount;

        private readonly Context _ctx;
        private readonly Program _program;
        private readonly Device _device;
        private readonly Kernel[] _kernels;
        private readonly CommandQueue _queue;

        public readonly string Source;
        public readonly uint KernelCount;
        public readonly string[] Methodes;

        static OpenClCompiler()
        {
             Cl.GetPlatformIDs(0, null, out PlatformCount);
             Platforms = new Platform[PlatformCount];
             Cl.GetPlatformIDs(PlatformCount, Platforms, out _);
        }

        public OpenClCompiler(string source, DeviceType type = DeviceType.Gpu)
            : this(Device.GetDevice(Platforms, type), source)
        {
        }
        public OpenClCompiler(string source, string name)
            : this(Device.GetDevice(Platforms, name), source)
        {
        }

        public OpenClCompiler(Device device, string source)
        {
            _device = device;
            _ctx = device.CreateContext();


            Source = source;
            _program = new Program(Cl.CreateProgramWithSource(_ctx, 1, new string[] { source }, null, out ErrorCode error));
            Cl.BuildProgram(_program, 1, new[] { _device }, string.Empty, null, IntPtr.Zero);

            KernelCount = _program.NumKernels;
            Methodes = _program.KernelNames;

            _kernels = new Kernel[KernelCount];

            if((error = Cl.CreateKernelsInProgram(_program, KernelCount, _kernels, out _)) != ErrorCode.Success) {
                throw new Exception($"{error}");
            }

            _queue = Cl.CreateCommandQueue(_ctx, _device, CommandQueueProperties.None, out _);
        }
        public SvmPointer AllocSvmMemory(long length)
        {
            SVMMemFlags flags = SVMMemFlags.ReadWrite;

            if (_device.IsFineGrainBufferSupported)
            {
                flags |= SVMMemFlags.FineGrainBuffer;
                if (_device.IsAtomicSupported)
                {
                    flags |= SVMMemFlags.Atomic;
                }

                return new SvmPointer(_ctx, Cl.SVMAlloc(_ctx, flags, new IntPtr(length)), length);
            }

            return new SvmPointer(_ctx, Cl.SVMAlloc(_ctx, flags, new IntPtr(length)), length);
        }
        public SvmPointer<T> AllocSvmMemory<T>(long count)
           where T : unmanaged
        {
            SVMMemFlags flags = SVMMemFlags.ReadWrite;

            long length = count * sizeof(T);
            if (_device.IsFineGrainBufferSupported)
            {
                flags |= SVMMemFlags.FineGrainBuffer;
                if (_device.IsAtomicSupported)
                {
                    flags |= SVMMemFlags.Atomic;
                }

                return (SvmPointer<T>) new SvmPointer(_ctx, Cl.SVMAlloc(_ctx, flags, new IntPtr(length)), length);

            } 
            return (SvmPointer<T>) new SvmPointer(_ctx, Cl.SVMAlloc(_ctx, flags, new IntPtr(length)), length);
        }
      
        /// <summary> 
        /// Get a spezific __kernel method from the compiled programm by name
        /// </summary>
        /// <param name="methodName">Methode name</param>
        /// <returns>Return a pointer to a kernel methode </returns>
        public OpenClBridge GetMethode(string methodName)
        {
            return new OpenClBridge(_ctx, _device, _kernels.First(kernel => kernel.FunctionName == methodName));
        }

       
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            foreach (Kernel kernel in _kernels)
            {
                kernel.Dispose();
            }

            _program.Dispose();
            _ctx.Dispose();
            _device.Dispose();

        }
    }
}
