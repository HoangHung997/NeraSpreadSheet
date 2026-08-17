# Third-party notices

NeraSpreadSheet uses third-party libraries through NuGet and contains small internal interoperability components adapted from permissively licensed upstream sources. This notice does not replace the license metadata shipped with each NuGet package.

## Vortice.Windows / Vortice.Wpf interoperability code

The following Nera files contain code adapted from the Vortice.Wpf D3D11-to-D3DImage interoperability implementation, with Nera-specific lifecycle, cleanup and validation changes:

- `src/NeraSpreadSheet.Wpf/NeraD3D9Interop.cs`
- `src/NeraSpreadSheet.Wpf/NeraD3D11ImageSurface.cs`

Upstream project: Vortice.Windows / Vortice.Wpf  
Copyright (c) Amer Koleci and Contributors

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
