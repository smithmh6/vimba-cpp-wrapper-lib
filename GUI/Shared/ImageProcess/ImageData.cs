using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using Thorlabs.CustomControls.TelerikAndSciChart.Controls.ColorMapEditor;

namespace FilterWheelShared.ImageProcess
{
    public class ImageData : IDisposable
    {
        private bool _disposed = false;

        public const double DpiX = 96.0;
        public const double DpiY = 96.0;

        private static int[] PixelBytes = { 1, 1, 2, 2, 4 };

        #region Properties
        private P2dInfo _dataInfo;
        public P2dInfo DataInfo => _dataInfo;
        public int Hdl { get; }
        public bool IsDisposed => _disposed;
        private bool FreeBufferAuto { get; } = true;
        #endregion

        #region Constructors

        public ImageData(int xSize, int ySize, P2dDataFormat dataFormat, int validBits, P2dChannels channel = P2dChannels.P2D_CHANNELS_1)
        {
            Hdl = P2DWrapper.CreateImage();
            P2DWrapper.InitImage(Hdl, xSize, ySize, dataFormat, channel, validBits);
            _dataInfo = new P2dInfo();
            P2DWrapper.GetInfo(Hdl, ref _dataInfo);
        }

        public ImageData(ImageInfo image_info, IntPtr buffer, bool freeBufferAuto = true)
        {
            var bitsPerPixel = 1;
            var channel = P2dChannels.P2D_CHANNELS_1;
            if (image_info.ImageType == DeviceDataService.ImageTypes.RGB)
            {
                bitsPerPixel = 3;
                channel = P2dChannels.P2D_CHANNELS_3;
            }

            if (image_info.LineBytes == 0)
            {
                image_info.LineBytes = image_info.Width * (uint)PixelBytes[(int)image_info.PixelType] * (uint)bitsPerPixel;
            }
            Hdl = P2DWrapper.CreateImage();
            _dataInfo = new P2dInfo
            {
                x_size = (int)image_info.Width,
                y_size = (int)image_info.Height,
                x_physical_um = 0,
                y_physical_um = 0,
                line_bytes = (int)image_info.LineBytes,
                pix_type = (P2dDataFormat)image_info.PixelType,
                valid_bits = image_info.validBits,
                channels = channel,
                data_buf = buffer
            };
            P2DWrapper.InitImageExternal(Hdl, ref _dataInfo);
            FreeBufferAuto = freeBufferAuto;
        }

        public ImageData(int xSize, int ySize, int stride, IntPtr buffer, P2dDataFormat dataFormat, int validBits, P2dChannels channels = P2dChannels.P2D_CHANNELS_1, bool freeBufferAuto = true)
        {
            Hdl = P2DWrapper.CreateImage();
            _dataInfo = new P2dInfo
            {
                x_size = xSize,
                y_size = ySize,
                x_physical_um = 0,
                y_physical_um = 0,
                line_bytes = stride,
                pix_type = dataFormat,
                valid_bits = validBits,
                channels = channels,
                data_buf = buffer
            };
            P2DWrapper.InitImageExternal(Hdl, ref _dataInfo);
            FreeBufferAuto = freeBufferAuto;
        }

        public ImageData(int handle)
        {
            Hdl = handle;
            _dataInfo = new P2dInfo();
            P2DWrapper.GetInfo(Hdl, ref _dataInfo);
        }
        #endregion
      
        public void UpdateWriteableBitmap(WriteableBitmap wbmp, CancellationToken token)
        {
            int width = DataInfo.x_size;
            int height = DataInfo.y_size;

            //IntPtr pBackBuffer = IntPtr.Zero;
            wbmp.Dispatcher?.Invoke(() =>
            {
                wbmp.Lock();
                //IntPtr pBackBuffer = wbmp.BackBuffer;
                //uint size = (uint)wbmp.BackBufferStride * (uint)wbmp.PixelHeight;
                //ZeroMemory(pBackBuffer, size);
                //}, System.Windows.Threading.DispatcherPriority.Render);
                //if (pBackBuffer == IntPtr.Zero)
                //    return false;
                
                sbyte status = 0;
                using (ImageData backBufferImg = new ImageData(width, height, wbmp.BackBufferStride, wbmp.BackBuffer, DataInfo.pix_type, DataInfo.valid_bits, DataInfo.channels))
                {
                    status = P2DWrapper.CopyImage(Hdl, backBufferImg.Hdl);
                }

                //wbmp.Dispatcher?.Invoke(() =>
                //{
                wbmp.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                wbmp.Unlock();
            }, System.Windows.Threading.DispatcherPriority.Render, token);

            //return status == 0;
        }

        public BitmapSource ToBitmapSource(int? dstMaxXY = null)
        {
            int width = DataInfo.x_size;
            int height = DataInfo.y_size;
            PixelFormat pixelFormat;
            if (DataInfo.channels == P2dChannels.P2D_CHANNELS_3)
            {
                if (DataInfo.pix_type != P2dDataFormat.P2D_8U)
                {
                    throw new Exception("ToBitmapSource : 3-channel data must be unsigned-8bit");
                }
                pixelFormat = PixelFormats.Rgb24;
            }
            else
            {
                switch (DataInfo.pix_type)
                {
                    case P2dDataFormat.P2D_8U:
                        pixelFormat = PixelFormats.Gray8;
                        break;
                    case P2dDataFormat.P2D_16U:
                        pixelFormat = PixelFormats.Gray16;
                        break;
                    default:
                        throw new Exception("ToBitmapSource : 1-channel data only support unsigned-8bit and unsigned-16bit");
                }
            }
            if (dstMaxXY != null)
            {
                int imgX = DataInfo.x_size;
                int imgY = DataInfo.y_size;
                if (imgX > imgY)
                {
                    imgY = (int)(dstMaxXY.Value * ((double)imgY / imgX));
                    imgX = dstMaxXY.Value;
                }
                else
                {
                    imgX = (int)(dstMaxXY.Value * ((double)imgX / imgY));
                    imgY = dstMaxXY.Value;
                }

                var ret = Resize(imgX, imgY, out ImageData resizedImage);
                if (ret != 0)
                {
                    throw new Exception($"ToBitmapSource : resize to {imgX} * {imgY} failed.");
                }
                var reInfo = resizedImage.DataInfo;
                BitmapSource rsBmp = BitmapSource.Create(reInfo.x_size, reInfo.y_size, DpiX, DpiY, pixelFormat, null, reInfo.data_buf, reInfo.line_bytes * reInfo.y_size, reInfo.line_bytes);
                return rsBmp;
            }
            BitmapSource bmp = BitmapSource.Create(width, height, DpiX, DpiY, pixelFormat, null, DataInfo.data_buf, DataInfo.line_bytes * height, DataInfo.line_bytes);
            return bmp;
        }

        //public ImageData ProcessWithColors(ThorColor[] colors)
        //{
        //    sbyte status;
        //    if (DataInfo.channels == P2dChannels.P2D_CHANNELS_1)
        //    {
        //        if (colors.Length != 1)
        //            throw new Exception("Gray image onlye need 1 color.");

        //        var color = colors[0];
        //        if (color == null)
        //        {
        //            color = (ThorColor)Colors.White;
        //        }
        //        switch (DataInfo.pix_type)
        //        {
        //            case P2dDataFormat.P2D_16U:
        //                var shiftBits = (DataInfo.valid_bits - 8)/* - cameraInstance.ExtraShiftBits*/;
        //                var mShiftValue = Math.Pow(2, -shiftBits);
        //                var imageData = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1);
        //                status = P2DWrapper.Scale(Hdl, imageData.Hdl, mShiftValue, 0);
        //                if (status != 0)
        //                {
        //                    imageData.Dispose();
        //                    throw new Exception("Shift image failed");
        //                }
        //                return imageData.ColorFilling(color);
        //            case P2dDataFormat.P2D_8U:
        //                return ColorFilling(color);
        //            default:
        //                throw new Exception($"Not support image type {DataInfo.pix_type}");
        //        }
        //    }
        //    else
        //    {
        //        ImageData outputImg = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3);
        //        if (colors.Length != 3)
        //            throw new Exception("RGB image need 3 colors.");

        //        var validChannelCount = colors.Count(c => c != null);
        //        var m_value = (double)1 / validChannelCount;
        //        double[] m_values = { m_value, m_value, m_value };

        //        try
        //        {
        //            using (
        //            ImageData rChannelImg = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1),
        //            gChannelImg = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1),
        //            bChannelImg = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1))
        //            {
        //                ImageData[] imgs = { rChannelImg, gChannelImg, bChannelImg };
        //                int[] hdls = imgs.Select(i => i.Hdl).ToArray();
        //                //status = P2DWrapper.CopyImage(outputImg.Hdl, hdls);
        //                //if (status != 0)
        //                //  throw new Exception($"{result}"); 

        //                for (int i = 0; i < 3; i++)
        //                {
        //                    ImageData img = imgs[i];
        //                    ThorColor color = colors[i];
        //                    if (color != null)
        //                    {
        //                        using (ImageData channelColorImg = img.ColorFilling(color))
        //                        {
        //                            status = P2DWrapper.Multiply(channelColorImg.Hdl, m_values);
        //                            if (status != 0)
        //                                throw new Exception("Multiply image failed.");
        //                            status = P2DWrapper.AddToImage(channelColorImg.Hdl, outputImg.Hdl);
        //                            if (status != 0)
        //                                throw new Exception("Combine image failed.");
        //                        }

        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            outputImg.Dispose();
        //            throw;
        //        }
        //        return outputImg;
        //    }
        //}

        public static ImageData TransferMonoImage(ImageData originalImage, int min, int max)
        {
            var dataInfo = originalImage.DataInfo;
            if (dataInfo.channels != P2dChannels.P2D_CHANNELS_1)
                throw new Exception("TransferMonoImage : only support 1-channel image data.");

            var imageData = new ImageData(dataInfo.x_size, dataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1);
            originalImage.ImageScale(imageData, new double[] { min }, new double[] { max });
            return imageData;
        }

        public static ImageData TransferMonoImageDataType(ImageData originalImage)
        {
            var dataInfo = originalImage.DataInfo;
            if (dataInfo.channels != P2dChannels.P2D_CHANNELS_1)
                throw new Exception("TransferMonoImageDataType : only support 1-channel image data.");

            sbyte status;
            switch (dataInfo.pix_type)
            {
                case P2dDataFormat.P2D_16U:
                    var shiftBits = (dataInfo.valid_bits - 8)/* - cameraInstance.ExtraShiftBits*/;
                    var mShiftValue = Math.Pow(2, -shiftBits);
                    var imageData = new ImageData(dataInfo.x_size, dataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1);
                    status = P2DWrapper.Scale(originalImage.Hdl, imageData.Hdl, mShiftValue, 0);
                    if (status != 0)
                    {
                        imageData.Dispose();
                        throw new Exception($"TransferMonoImageDataType : Shift image failed. Error {status}.");
                    }
                    //originalImage.Dispose();
                    return imageData;

                case P2dDataFormat.P2D_8U:
                    return Copy(originalImage);
                default:
                    throw new Exception($"TransferMonoImageDataType : Not support image type {dataInfo.pix_type}.");
            }
        }

        public static void CopyColorToRGBChannelImages(ImageData originalImage, ImageData rChannelImage, ImageData gChannelImage, ImageData bChannelImage)
        {
            int[] hdls = { rChannelImage.Hdl, gChannelImage.Hdl, bChannelImage.Hdl };

            var status = P2DWrapper.CopyColorTo3Channels(originalImage.Hdl, hdls);
            if (status != 0)
                throw new Exception($"CopyColorToRGBChannelImages failed. Error {status}.");
        }

        public static void ProcessWithFlipI(ImageData src, bool isFlipH, bool isFlipV)
        {
            if (src.DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
                throw new Exception("ProcessWithFlipI : Only single-channel image support Flip.");

            if (!isFlipH && !isFlipV)
                return;

            if (isFlipV && !isFlipH)
            {
                src.ImageFlipI(P2dAxis.P2D_AxsHorizontal);
            }
            if (!isFlipV && isFlipH)
            {
                src.ImageFlipI(P2dAxis.P2D_AxsVertical);
            }
            if (isFlipV && isFlipH)
            {
                src.ImageFlipI(P2dAxis.P2D_AxsBoth);
            }
        }

        public static ImageData ProcessWithColor(ImageData src, ThorColor color, bool disposeSrc = true)
        {
            if (color == null)
                return null;

            if (src == null)
                throw new Exception("ProcessWithColor : Source image data is empty.");

            var dst = src.ColorFilling(color);
            if (disposeSrc)
                src.Dispose();

            return dst;
        }

        public static void ProcessColorWithLUTNew(ImageData src, ImageData dst, ThorColor color, byte[] colorTable, bool disposeSrc = true)
        {
            if (colorTable == null) return;

            if (src == null)
                throw new Exception("ProcessColorWithLUT : Source image data is empty.");

            src.ColorLookUpTableNew(dst, color, colorTable);
        }

        public static void ProcessWithMinMax(ImageData src, ImageData dst, int min, int max)
        {
            if (src == null)
                throw new Exception("ProcessWithMinMax : Source image data is empty.");
            if (dst == null)
                throw new Exception("ProcessWithMinMax : Destination image data is empty.");

            src.ImageAdjust(dst, min, max);
        }

        public static void ProcessWithMinMaxNew(ImageData src, ImageData dst, int min, int max)
        {
            if (src == null)
                throw new Exception("ProcessWithMinMax : Source image data is empty.");
            if (dst == null)
                throw new Exception("ProcessWithMinMax : Destination image data is empty.");

            src.ImageAdjustNew(dst, min, max);
        }

        public static void ProcessWithMinMaxI(ImageData src, int min, int max)
        {
            if (src == null)
                throw new Exception("ProcessWithMinMaxI : Source image data is empty.");

            if (src.DataInfo.channels != P2dChannels.P2D_CHANNELS_1 || src.DataInfo.pix_type != P2dDataFormat.P2D_8U)
                throw new Exception("ProcessWithMinMaxI : Source image must be unsigned 8bit 1 channel.");

            if (min != 0 || max != 255)
            {
                double c = 255.0 / (max - min);
                double b = 0 - (min * c);
                sbyte status = P2DWrapper.ScaleI(src.Hdl, c, b);
                if (status != 0)
                    throw new Exception($"ProcessWithMinMaxI : Scale image failed. Error {status}.");
            }
        }       

        public static void GammaCorrection(ImageData src, ImageData dst, byte[] gammaTable)
        {
            if (src == null)
                throw new Exception("GammaCorrection : Source image data is empty.");

            src.GammaLookUpTable(dst, gammaTable);
        }


        public static ImageData Combine(ImageData[] images, bool disposeSrc = true)
        {
            if (images.Length < 1)
                return null;
            var m_value = 1.0 / images.Length;
            double[] m_values = { m_value, m_value, m_value };

            var dataInfo = images[0].DataInfo;
            ImageData outputImage = new ImageData(dataInfo.x_size, dataInfo.y_size, dataInfo.pix_type, dataInfo.valid_bits, dataInfo.channels);

            for (int i = 0; i < images.Length; i++)
            {
                ImageData img = images[i];
                if (img.DataInfo.channels != P2dChannels.P2D_CHANNELS_3)
                    throw new Exception("Combine : Element must be 3-channels data.");
                //var status = P2DWrapper.Multiply(img.Hdl, m_values);
                //if (status != 0)
                //    throw new Exception("Myltiply failed.");

                var status = P2DWrapper.AddToImage(img.Hdl, outputImage.Hdl);


                if (status != 0)
                    throw new Exception($"Combine : Add to image failed. Error {status}.");
            }

            return outputImage;
        }


        public static void Combine(ImageData[] images, ImageData combineImg, bool disposeSrc = true)
        {
            if (images.Length < 1)
                return;
            var m_value = 1.0 / images.Length;
            double[] m_values = { m_value, m_value, m_value };

            for (int i = 0; i < images.Length; i++)
            {
                ImageData img = images[i];
                if (img.DataInfo.channels != P2dChannels.P2D_CHANNELS_3)
                    throw new Exception("Combine : Element must be 3-channels data.");
                //var status = P2DWrapper.Multiply(img.Hdl, m_values);
                //if (status != 0)
                //    throw new Exception("Myltiply failed.");

                var status = P2DWrapper.AddToImage(img.Hdl, combineImg.Hdl);

                if (status != 0)
                    throw new Exception($"Combine : Add to image failed. Error {status}.");
            }
        }

        public static void AddCombineImage(ImageData srcImage, ImageData dstImage)
        {
            P2DWrapper.AddToImage(srcImage.Hdl, dstImage.Hdl);
        }       

        public void ColorLookUpTableNew(ImageData dstImg, ThorColor color, byte[] colorTable)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
            {
                throw new Exception("ColorLookUpTable : Only single-channel image support.");
            }
            if (DataInfo.pix_type != P2dDataFormat.P2D_8U)
            {
                throw new Exception("ColorLookUpTable : Only 8u data support ColorLookUpTable.");
            }
            if (color == null)
            {
                color = ThorColorService.GetInstance().GrayColor;
            }

            sbyte status;
            int channel = 0;
            var result = IsSingleChannelThorColor(color, ref channel);
            if (result)
            {
                status = P2DWrapper.CopyGrayToColor(Hdl, dstImg.Hdl, channel);
                if (status != 0)
                    throw new Exception($"ColorLookUpTable : Copy gray to color failed. Error {status}.");
            }
            else if (color == ThorColorService.GetInstance().GrayColor)
            {
                status = P2DWrapper.GrayToColor(Hdl, dstImg.Hdl);
                if (status != 0)
                    throw new Exception($"ColorLookUpTable : Gray to color failed. Error {status}.");
            }
            else
            {
                status = P2DWrapper.ColorLookUpTableNew(Hdl, dstImg.Hdl, colorTable);
                if (status != 0)
                    throw new Exception($"ColorLookUpTable : Computer color image failed. Error {status}.");
            }
        }

        public void GammaLookUpTable(ImageData dstImg, byte[] gammaTable)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
            {
                throw new Exception("GammaLookUpTable : Only single-channel image support.");
            }
            if (DataInfo.pix_type != P2dDataFormat.P2D_8U)
            {
                throw new Exception("GammaLookUpTable : Only 8u data support gammaTable.");
            }
            if (gammaTable == null)
            {
                throw new Exception("GammaLookUpTable : GammaTable should not empty.");
            }

            var status = P2DWrapper.ColorLookUpTableNew(Hdl, dstImg.Hdl, gammaTable);
            if (status != 0)
                throw new Exception($"GammaLookUpTable : Computer gamma image failed. Error {status}.");

        }

        public static sbyte FlipI(ImageData src, bool hFlip, bool vFlip)
        {
            if (!hFlip && !vFlip)
                return 0;

            P2dAxis flipAxis;
            if (hFlip && vFlip)
            {
                flipAxis = P2dAxis.P2D_AxsBoth;
            }
            else
            {
                if (vFlip)
                    flipAxis = P2dAxis.P2D_AxsHorizontal;
                else
                    flipAxis = P2dAxis.P2D_AxsVertical;
            }
            return P2DWrapper.MirrorI(src.Hdl, flipAxis);
        }

        public void CopyTo(ImageData dst)
        {
            var dataInfo = dst.DataInfo;
            var status = P2DWrapper.CopyImage(Hdl, dst.Hdl);
            if (status != 0)
            {
                dst.Dispose();
                throw new Exception($"CopyTo : Copy image failed. Error {status}.");
            }
        }

        public static ImageData Copy(ImageData src)
        {
            var dataInfo = src.DataInfo;
            ImageData tempImage = new ImageData(dataInfo.x_size, dataInfo.y_size, dataInfo.pix_type, dataInfo.valid_bits, dataInfo.channels);
            var status = P2DWrapper.CopyImage(src.Hdl, tempImage.Hdl);
            if (status != 0)
            {
                tempImage.Dispose();
                throw new Exception($"Copy : Copy image failed. Error {status}.");
            }
            return tempImage;
        }

        private static bool IsSingleChannelThorColor(ThorColor color, ref int channel)
        {
            var instance = ThorColorService.GetInstance();
            if (color == instance.RedColor)
            {
                channel = 0;
                return true;
            }
            if (color == instance.GreenColor)
            {
                channel = 1;
                return true;
            }
            if (color == instance.BlueColor)
            {
                channel = 2;
                return true;
            }
            return false;
        }

        public ImageData ColorFilling(ThorColor color)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
            {
                throw new Exception("ColorFilling : Only single-channel image support.");
            }
            if (DataInfo.pix_type != P2dDataFormat.P2D_8U)
            {
                throw new Exception("ColorFilling : Only 8u data support ColorFilling.");
            }
            if (color == null)
            {
                color = ThorColorService.GetInstance().GrayColor;
            }

            sbyte status;
            ImageData tempImg = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3);
            int channel = 0;
            if (IsSingleChannelThorColor(color, ref channel))
            {
                status = P2DWrapper.CopyGrayToColor(Hdl, tempImg.Hdl, channel);
                if (status != 0)
                {
                    tempImg.Dispose();
                    throw new Exception($"ColorFilling : Copy gray to color failed. Error {status}.");
                }
            }
            else
            {
                //using (ImageData colorImg = new ImageData(DataInfo.x_size, DataInfo.y_size, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_3))
                //{
                //status = P2DWrapper.GrayToColor(Hdl, colorImg.Hdl);
                //if (status != 0)
                //{
                //    tempImg.Dispose();
                //    throw new Exception("Convert gray to color failed!");
                //}
                var srcRect = new P2dRect { x = 0, y = 0, width = DataInfo.x_size, height = DataInfo.y_size };
                //status = P2DWrapper.ComputerColorBC(colorImg.Hdl, tempImg.Hdl, srcRect, color.Data.ToArray(), 1.0, 0);
                status = P2DWrapper.ComputerColorBC(Hdl, tempImg.Hdl, srcRect, color.Data.ToArray(), 1.0, 0);
                if (status != 0)
                {
                    tempImg.Dispose();
                    throw new Exception($"ColorFilling : Computer color image failed. Error {status}.");
                }
                //}
            }
            return tempImg;
        }

        public void ImageFlipI(P2dAxis flipAxis)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
            {
                throw new Exception("ImageFlipI : Only single-channel image support Flip.");
            }

            var status = P2DWrapper.MirrorI(Hdl, flipAxis);
            if (status != 0)
            {
                throw new Exception($"ImageFlipI : Flip failed. Error {status}.");
            }
        }

        public void ImageScale(ImageData dstImg, double[] min, double[] max)
        {
            sbyte status;
            if (dstImg.DataInfo.channels == P2dChannels.P2D_CHANNELS_1)
            {
                switch (dstImg.DataInfo.pix_type)
                {
                    case P2dDataFormat.P2D_8U:
                        if (min[0] != 0 || max[0] != 255)
                        {
                            double c = 255 / (max[0] - min[0]);
                            double b = 0 - (min[0] * c);
                            status = P2DWrapper.Scale(Hdl, dstImg.Hdl, c, b);
                            if (status != 0)
                            {
                                dstImg.Dispose();
                                throw new Exception($"ImageScale : Adjust image failed. Error {status}.");
                            }
                        }
                        else
                        {
                            this.CopyTo(dstImg);
                        }
                        break;
                    case P2dDataFormat.P2D_16U:
                        var shiftBits = (DataInfo.valid_bits - 8)/* - cameraInstance.ExtraShiftBits*/;
                        var mShiftValue = Math.Pow(2, -shiftBits);
                        if (min[0] != 0 || max[0] != 255)
                        {
                            double c = 255 / (max[0] - min[0]);
                            double b = 0 - (min[0] * c);
                            //(A * C1 + B1) * C2 + B2
                            double m_value = c * mShiftValue;
                            double a_value = 0 * mShiftValue + b;
                            //use scale instead of RShift
                            //combine shift(scale) and scale into one step
                            status = P2DWrapper.Scale(Hdl, dstImg.Hdl, m_value, a_value);
                            //status = P2DWrapper.ShiftRBuffer(Hdl, temp8UImg.Hdl, cameraInstance.ShiftBits);
                            if (status != 0)
                            {
                                dstImg.Dispose();
                                throw new Exception($"ImageScale : Shift image failed with min and max. Error {status}.");
                            }
                        }
                        else
                        {
                            //use scale instead of RShift
                            status = P2DWrapper.Scale(Hdl, dstImg.Hdl, mShiftValue, 0);
                            //status = P2DWrapper.ShiftRBuffer(Hdl, imageData.Hdl, cameraInstance.ShiftBits);
                            if (status != 0)
                            {
                                dstImg.Dispose();
                                throw new Exception($"ImageScale : Shift image failed. Error {status}.");
                            }

                        }
                        break;
                    default:
                        throw new Exception($"ImageScale : Not support image type {DataInfo.pix_type}.");
                }
            }
            else
            {
                if (min.Any(v => v != 0) || max.Any(v => v != 255))
                {
                    status = P2DWrapper.Adjust(Hdl, dstImg.Hdl, min, max);
                    if (status != 0)
                    {
                        dstImg.Dispose();
                        throw new Exception($"ImageScale : Adjust image failed. Error {status}.");
                    }
                }
                else
                {
                    throw new Exception($"ImageScale : Not support image type {DataInfo.pix_type}.");
                }
            }
        }

        public void ImageAdjust(ImageData dst, double min, double max)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
                throw new Exception($"ImageAdjust : Adjust only support 1channel image.");

            if (DataInfo.x_size != dst.DataInfo.x_size || DataInfo.y_size != dst.DataInfo.y_size)
                throw new Exception("ImageAdjust : Source image size is different from destination image size.");

            sbyte status;
            switch (DataInfo.pix_type)
            {
                case P2dDataFormat.P2D_8U:
                    if (min != 0 || max != 255)
                    {
                        double c = 255 / (max - min);
                        double b = 0 - (min * c);
                        status = P2DWrapper.Scale(Hdl, dst.Hdl, c, b);
                        if (status != 0)
                            throw new Exception($"ImageAdjust : Adjust image failed. Error {status}.");
                    }
                    else
                    {
                        status = P2DWrapper.CopyImage(Hdl, dst.Hdl);
                        if (status != 0)
                            throw new Exception($"ImageAdjust : Copy image failed. Error {status}.");
                    }
                    break;
                case P2dDataFormat.P2D_16U:
                    var max_value = (1 << DataInfo.valid_bits) - 1;
                    var shiftBits = (DataInfo.valid_bits - 8);
                    double shift_m_value = Math.Pow(2, -shiftBits);

                    var expand_max = (max_value / 255.0) * max;
                    var expand_min = (max_value / 255.0) * min;
                    double m_value = max_value / (expand_max - expand_min);
                    double a_value = 0 - (expand_min * m_value);
                    m_value *= shift_m_value;
                    a_value *= shift_m_value;

                    status = P2DWrapper.Scale(Hdl, dst.Hdl, m_value, a_value);
                    if (status != 0)
                    {
                        throw new Exception($"ImageAdjust : Scale image failed with min and max. Error {status}.");
                    }
                    break;
                default:
                    throw new Exception($"ImageAdjust : Not support image type {DataInfo.pix_type}.");
            }
        }

        public void ImageAdjustNew(ImageData dst, double min, double max)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
                throw new Exception($"ImageAdjust : Adjust only support 1channel image.");
            if (DataInfo.channels != dst.DataInfo.channels)
                throw new Exception($"ImageAdjust : Adjust only support same channel image.");
            if (DataInfo.x_size != dst.DataInfo.x_size || DataInfo.y_size != dst.DataInfo.y_size)
                throw new Exception("ImageAdjust : Source image size is different from destination image size.");

            sbyte status;
            var topV = (1 << DataInfo.valid_bits) - 1;
            if (max > topV)
                max = topV;
            if (min == 0 && Math.Abs(max - topV) < 1e-6)
            {
                status = P2DWrapper.CopyImage(Hdl, dst.Hdl);
                if (status != 0)
                    throw new Exception($"ImageAdjust : Copy image failed. Error {status}.");
            }
            else
            {
                double m_value = topV / (max - min);
                double a_value = 0 - (min * m_value);
                status = P2DWrapper.Scale(Hdl, dst.Hdl, m_value, a_value);
                if (status != 0)
                {
                    throw new Exception($"ImageAdjust : Scale image failed with min and max. Error {status}.");
                }
            }
        }

        public void ImageScale16uTo8u(ImageData dst, double min, double max)
        {
            if (DataInfo.channels != P2dChannels.P2D_CHANNELS_1)
                throw new Exception($"ImageAdjust : Adjust only support 1channel image.");
            if (DataInfo.channels != dst.DataInfo.channels)
                throw new Exception($"ImageAdjust : Adjust only support same channel image.");
            if (DataInfo.x_size != dst.DataInfo.x_size || DataInfo.y_size != dst.DataInfo.y_size)
                throw new Exception("ImageAdjust : Source image size is different from destination image size.");

            var max_value = (1 << DataInfo.valid_bits) - 1;
            var shiftBits = (DataInfo.valid_bits - 8);
            double shift_m_value = Math.Pow(2, -shiftBits);

            var expand_max = (max_value / 255.0) * max;
            var expand_min = (max_value / 255.0) * min;
            double m_value = max_value / (expand_max - expand_min);
            double a_value = 0 - (expand_min * m_value);
            m_value *= shift_m_value;
            a_value *= shift_m_value;

            var status = P2DWrapper.Scale(Hdl, dst.Hdl, m_value, a_value);
            if (status != 0)
            {
                throw new Exception($"ImageAdjust : Scale image failed with min and max. Error {status}.");
            }
        }

        //public void ImageAdjustI(double min, double max)
        //{
        //    sbyte status;
        //    if (DataInfo.channels == P2dChannels.P2D_CHANNELS_1)
        //    {
        //        switch (DataInfo.pix_type)
        //        {
        //            case P2dDataFormat.P2D_8U:
        //                if (min != 0 || max != 255)
        //                {
        //                    double c = 255 / (max - min);
        //                    double b = 0 - (min * c);
        //                    status = P2DWrapper.ScaleI(Hdl, c, b);
        //                    if (status != 0)                                                            
        //                        throw new Exception($"ImageAdjust : Adjust image failed. Error {status}.");                           
        //                }                       
        //                break;
        //            case P2dDataFormat.P2D_16U:
        //                var shiftBits = (DataInfo.valid_bits - 8)/* - cameraInstance.ExtraShiftBits*/;
        //                var mShiftValue = Math.Pow(2, -shiftBits);
        //                if (min != 0 || max != 255)
        //                {
        //                    double c = 255 / (max - min);
        //                    double b = 0 - (min * c);
        //                    //(A * C1 + B1) * C2 + B2
        //                    double m_value = c * mShiftValue;
        //                    double a_value = 0 * mShiftValue + b;
        //                    //use scale instead of RShift
        //                    //combine shift(scale) and scale into one step
        //                    status = P2DWrapper.ScaleI(Hdl, m_value, a_value);
        //                    //status = P2DWrapper.ShiftRBuffer(Hdl, temp8UImg.Hdl, cameraInstance.ShiftBits);
        //                    if (status != 0)
        //                    {
        //                        throw new Exception($"ImageAdjust : Shift image failed with min and max. Error {status}.");
        //                    }
        //                }
        //                //else
        //                //{
        //                //    //use scale instead of RShift
        //                //    status = P2DWrapper.Scale(Hdl, imageData.Hdl, mShiftValue, 0);
        //                //    //status = P2DWrapper.ShiftRBuffer(Hdl, imageData.Hdl, cameraInstance.ShiftBits);
        //                //    if (status != 0)
        //                //    {
        //                //        imageData.Dispose();
        //                //        throw new Exception($"ImageAdjust : Shift image failed. Error {status}.");
        //                //    }

        //                //}
        //                break;
        //            default:
        //                throw new Exception($"ImageAdjust : Not support image type {DataInfo.pix_type}.");
        //        }
        //    }           
        //}

        public void ImageScaleI(double contrast = 1.0, double brightness = 0.0)
        {
            if (DataInfo.channels == P2dChannels.P2D_CHANNELS_1)
            {
                if (contrast == 1.0 && brightness == 0)
                    return;
                sbyte status = P2DWrapper.ScaleI(Hdl, contrast, brightness);
                if (status != 0)
                    throw new Exception($"Update brightness and contrast failed. Error {status}.");
            }
        }

        public sbyte GetHistogram(ref uint[] histogram, float max = 256)
        {
            var histogrameSize = (uint)histogram.Length;
            //uint[] histogram = new uint[histogrameSize];
            if (DataInfo.channels == P2dChannels.P2D_CHANNELS_1)
                return P2DWrapper.GetHistogram(Hdl, 0, max, histogram, histogrameSize);

            throw new Exception("GetHistogram : 3-channel data not support histogram.");
            //else
            //{
            //    int width = DataInfo.x_size;
            //    int height = DataInfo.y_size;
            //    ImageData grayImg = new ImageData(width, height, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1);
            //    P2DWrapper.ColorToGray(Hdl, grayImg.Hdl);
            //    P2DWrapper.GetHistogram(grayImg.Hdl, 0, max, histogram, histogrameSize);
            //    grayImg.Dispose();
            //}
            //return histogram;
        }

        //p2d_get_min/max get a array of three channel
        //and the value type is dependent on image data type

        public sbyte GeProfile(P2dPoint start, P2dPoint end, int lineWidth, ushort[] profile, ref int length)
        {

            uint l = (uint)profile.Length;

            sbyte result = 0;
            switch (DataInfo.pix_type)
            {
                case P2dDataFormat.P2D_8U:
                    var buff8u = new byte[l];
                    result = P2DWrapper.P2dGetFastProfile8u(Hdl, start, end, (uint)lineWidth, buff8u, ref l);
                    buff8u.CopyTo(profile, 0);
                    break;
                case P2dDataFormat.P2D_16U:
                    result = P2DWrapper.P2dGetFastProfile16u(Hdl, start, end, (uint)lineWidth, profile, ref l);
                    break;
            }
            length = (int)l;
            return result;
        }

        public sbyte GetROIStatistic(IEnumerable<StatisticItem> statisticItems, ChannelType ct, Tuple<decimal, decimal> ratio)
        {
            var items = statisticItems.Where(i => i.ChannelType == ct).ToList();
            foreach (var item in items)
            {
                //physical
                var physical_w = item.ROIPhysical.Width * (double)ratio.Item1;
                var physical_h = item.ROIPhysical.Height * (double)ratio.Item2;
                double area = 0, perimeter = 0;
                switch (item.ROIType)
                {
                    case P2dRoiType.P2D_Ellipse:
                        area = Math.PI * physical_w * physical_h / 4;
                        var b = Math.Min(physical_w, physical_h);
                        var a = Math.Max(physical_w, physical_h);
                        var h = Math.Pow(b - a, 2) / Math.Pow(b + a, 2);
                        //l≈π(a+b)(1+3h/(10+√(4-3h))
                        perimeter = Math.PI * (a + b) * (1 + ((3 * h) / (10 + Math.Pow(4 - 3 * h, 0.5))));
                        //perimeter = 2 * Math.PI * b + 4 * (a - b);
                        break;
                    case P2dRoiType.P2D_Rectangle:
                        area = physical_w * physical_h;
                        perimeter = 2 * physical_w + 2 * physical_h;
                        break;
                }
                var digits = (int)Math.Floor(Math.Log10(area));
                var temp = digits / 6;
                var dstUnit = PhysicalUnit.um;
                if (temp < 3)
                    dstUnit += temp;
                else
                    dstUnit = PhysicalUnit.m;

                var multiArea = Math.Pow(1e6, dstUnit - PhysicalUnit.um);
                var multiPeri = Math.Pow(1e3, dstUnit - PhysicalUnit.um);

                item.Unit = dstUnit;
                item.Area = Math.Round(area / multiArea, 2, MidpointRounding.AwayFromZero);
                item.Perimeter = Math.Round(perimeter / multiPeri, 2, MidpointRounding.AwayFromZero);

                //pixel
                var pixel_x = item.ROIPixel.x;
                var pixel_y = item.ROIPixel.y;
                var pixel_w = item.ROIPixel.width;
                var pixel_h = item.ROIPixel.height;
                using (ImageData maskImg = new ImageData(pixel_w, pixel_h, P2dDataFormat.P2D_8U, 8, P2dChannels.P2D_CHANNELS_1))
                {
                    var pStart = new P2dPoint();
                    pStart.x = 0;
                    pStart.y = 0;
                    var pWh = new P2dPoint();
                    pWh.x = pixel_w;
                    pWh.y = pixel_h;
                    P2dPoint[] pl = { pStart, pWh };
                    var status = P2DWrapper.GenerateMask(maskImg.Hdl, item.ROIType, pl, 2);
                    float min, max;
                    status = P2DWrapper.MaskMinMax(Hdl, maskImg.Hdl, pixel_x, pixel_y, out min, out max);
                    double mean, stddev;
                    status = P2DWrapper.MaskMeanStddev(Hdl, maskImg.Hdl, pixel_x, pixel_y, out mean, out stddev);
                    item.Min = (ushort)min;
                    item.Max = (ushort)max;
                    item.Mean = Math.Round(mean, 2, MidpointRounding.AwayFromZero);
                    item.StdDev = Math.Round(stddev, 2, MidpointRounding.AwayFromZero);
                }
            }
            return 0;
        }

        public sbyte GetMinMaxMono(out ushort min, out ushort max)
        {
            sbyte status = 0;
            min = 0;
            max = 255;
            switch (DataInfo.pix_type)
            {
                case P2dDataFormat.P2D_8U:
                    byte[] bmin = new byte[1];
                    byte[] bmax = new byte[1];
                    status = P2DWrapper.GetMinMax_8u(Hdl, bmin, bmax);
                    min = bmin[0];
                    max = bmax[0];
                    break;
                case P2dDataFormat.P2D_16U:
                    ushort[] umin = new ushort[1];
                    ushort[] umax = new ushort[1];
                    status = P2DWrapper.GetMinMax_16u(Hdl, umin, umax);
                    min = umin[0];
                    max = umax[0];
                    break;
            }
            return status;
        }

        public sbyte GetMinMaxColor(out byte[] min, out byte[] max)
        {
            min = new byte[3] { 0, 0, 0 }; ;
            max = new byte[3] { 255, 255, 255 }; ;
            if (DataInfo.pix_type != P2dDataFormat.P2D_8U) return -1;
            byte[] bmin = new byte[3];
            byte[] bmax = new byte[3];
            var status = P2DWrapper.GetMinMax_8u(Hdl, bmin, bmax);
            min = bmin;
            max = bmax;
            return status;
        }
        public sbyte GetMean(System.Windows.Rect rect, ref double mean)
        {
            P2dRect p2dRect = new P2dRect()
            {
                x = (int)rect.X,
                y = (int)rect.Y,
                width = (int)rect.Width,
                height = (int)rect.Height,
            };
            double sum = 0;
            var status = P2DWrapper.SumRectImage(Hdl, p2dRect, ref sum);
            int totalPoints = p2dRect.width * p2dRect.height;
            mean = sum / totalPoints;
            return status;
        }

        public sbyte ConvertTo(P2dDataFormat format, int validBits, out ImageData convertedImage)
        {
            convertedImage = new ImageData(_dataInfo.x_size, _dataInfo.y_size, format, validBits, _dataInfo.channels);
            return P2DWrapper.Convert(Hdl, convertedImage.Hdl);
        }

        public static sbyte GrayToColor(ImageData srcImg, ImageData dstImage, int channel)
        {
            return P2DWrapper.CopyGrayToColor(srcImg.Hdl, dstImage.Hdl, channel);
        }

        public static int LoadTiffFile(string filePath, ref int fileHandle, ref uint imageCount)
        {
            return CameraLIbCommand.GetImageCount(filePath, ref fileHandle, ref imageCount);
        }

        public static int LoadTiffImage(int fileHandle, uint imageIndex, out TiffImageSimpleInfo info)
        {
            info = new TiffImageSimpleInfo();
            return CameraLIbCommand.GetImageData(fileHandle, imageIndex, ref info);
        }

        public static void CloseTiffFile(int fileHandle)
        {
            if (fileHandle < 0) return;
            CameraLIbCommand.CloseImage(fileHandle);
        }

        public static int LoadImage(string filePath, ref int fileHandle, ref uint imageCount, out TiffImageSimpleInfo info, bool closeFileWhatever = false)
        {
            info = new TiffImageSimpleInfo();
            bool loadSuccess = false;
            var extension = System.IO.Path.GetExtension(filePath);
            switch (extension)
            {
                case ".tif":
                case ".tiff":
                    {
                        fileHandle = -1;
                        do
                        {
                            var result = CameraLIbCommand.GetImageCount(filePath, ref fileHandle, ref imageCount);
                            if (result != 0)
                            {
                                break;
                            }
                            result = CameraLIbCommand.GetImageData(fileHandle, 0, ref info);
                            if (result != 0)
                            {
                                break;
                            }
                            loadSuccess = true;
                        }
                        while (false);

                        if (loadSuccess)
                        {
                            if (closeFileWhatever)
                                CameraLIbCommand.CloseImage(fileHandle);
                            else
                            {
                                if (imageCount == 1)
                                    CameraLIbCommand.CloseImage(fileHandle);
                            }
                        }
                        else
                        {
                            if (fileHandle >= 0)
                                CameraLIbCommand.CloseImage(fileHandle);
                        }
                    }
                    break;
                default:
                    break;
            }
            if (loadSuccess)
            {
                return info.p2d_img_hdl;
            }

            return -1;
        }

        public static sbyte ResizeImg(ImageData src,ImageData dst)
        {
            if (src.DataInfo.channels != dst.DataInfo.channels || src.DataInfo.valid_bits != dst.DataInfo.valid_bits) return -1;
            var point = new P2dPoint { x = 0, y = 0 };
            var srcRegion = new P2dRegion { w = src.DataInfo.x_size, h = src.DataInfo.y_size };
            var dstRegion = new P2dRegion { w = dst.DataInfo.x_size, h = dst.DataInfo.y_size };
            return P2DWrapper.Resize(src.Hdl, point, srcRegion, dst.Hdl, point, dstRegion, P2dInterpolationType.P2D_Nearest);
        }

        public sbyte GetOnePixelData(int x, int y, out int pixelV)
        {
            if (x < 0) x = 0;
            if (x >= DataInfo.x_size) x = DataInfo.x_size - 1;
            if (y < 0) y = 0;
            if (y >= DataInfo.y_size) y = DataInfo.y_size - 1;
            P2dPoint point = new P2dPoint() { x = x, y = y };
            sbyte status = 0;
            pixelV = 0;
            switch (DataInfo.pix_type)
            {
                case P2dDataFormat.P2D_8U:
                    {
                        byte pv = 0;
                        status = P2DWrapper.GetOnePixel8U(Hdl, point, out pv);
                        pixelV = pv;
                    }
                    break;
                case P2dDataFormat.P2D_16U:
                    {
                        ushort pv = 0;
                        status = P2DWrapper.GetOnePixel16U(Hdl, point, out pv);
                        pixelV = pv;
                        break;
                    }
            }
            return status;
        }

        public sbyte Reset()
        {
            return P2DWrapper.Reset(Hdl);
        }

        internal sbyte Resize(int width, int height, out ImageData resizedImage)
        {
            resizedImage = new ImageData(width, height, _dataInfo.pix_type, _dataInfo.valid_bits, _dataInfo.channels);
            var point = new P2dPoint { x = 0, y = 0 };
            var srcRegion = new P2dRegion { w = _dataInfo.x_size, h = _dataInfo.y_size };
            var dstRegion = new P2dRegion { w = width, h = height };
            return P2DWrapper.Resize(Hdl, point, srcRegion, resizedImage.Hdl, point, dstRegion);
        }

        internal sbyte ScaleTo8bitByChannel(double ch1Max, double ch2Max, double ch3Max)
        {
            var ch1Scale = ch1Max > 255 ? 255 / ch1Max : 1.0;
            var ch2Scale = ch2Max > 255 ? 255 / ch2Max : 1.0;
            var ch3Scale = ch3Max > 255 ? 255 / ch3Max : 1.0;
            return P2DWrapper.Multiply(Hdl, new double[] { ch1Scale, ch2Scale, ch3Scale });
        }

        #region Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (!FreeBufferAuto)
                    Marshal.FreeHGlobal(DataInfo.data_buf);
                P2DWrapper.FreeImage(Hdl);
            }
            _disposed = true;
        }

        ~ImageData()
        {
            Dispose(false);
        }
        #endregion

        [DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        internal static extern void ZeroMemory(IntPtr dest, uint size);
    }

}
