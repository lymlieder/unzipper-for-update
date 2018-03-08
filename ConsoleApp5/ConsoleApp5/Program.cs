using System;
using System.IO;
using System.Collections.Generic;

namespace ConsoleApp5
{
    class Program
    {
        static FileStream fileResultReader, fileBaseReader, fileWriter;
        static Int64 addressLength, tempLength;
        static List<byte> fileList;
        static int diff = 0;//这个参数非常重要，因为差异包里所有的地址描述都是以旧文件为准的，而在文件替换过程中会有增加和删除的部分，所有地址会有飘逸，而这个diff就是总的地址漂移量。
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            fileBaseReader = new FileStream(@"C:\Users\Master\Desktop\upload\Test V17.bin", FileMode.Open, FileAccess.Read);
            fileResultReader = new FileStream(@"C:\Users\Master\Desktop\App1 Result.bin", FileMode.Open, FileAccess.Read);
            
            fileList = new List<byte>();
            BuildFileArray();
            fileResultReader.Position = 0;
            if (fileList.Count != fileBaseReader.Length)
            {
                Console.WriteLine(@"转录list元素个数错误");
                return;
            }
            fileBaseReader.Close();

            fileWriter = new FileStream(@"C:\Users\Master\Desktop\App5 fileWriter.bin", FileMode.Create, FileAccess.ReadWrite);
            BuiltFileList();

            fileResultReader.Position = 0;
            WriteToFile();
            fileResultReader.Close();
            fileWriter.Close();
        }

        static void BuildFileArray()//读取tempFile
        {
            fileList.Clear();
            int temp = 0;//把old转录到tempFile中
            while ((temp = fileBaseReader.ReadByte()) != -1)
                fileList.Add((byte)temp);

            //修改tempFile
            fileResultReader.Position = 32;
            addressLength = fileResultReader.ReadByte();//获得地址长度

            {//第一次循环，替换单双元素
                int tempType = 0;
                while ((tempType = fileResultReader.ReadByte()) != -1)
                {
                    //先替换aa,bb开头的
                    if (tempType == 0xff)//先跳过ff部分
                    {
                        switch (fileResultReader.ReadByte())
                        {
                            case 0x01://替换
                                fileResultReader.Position += addressLength;//跳过地址
                                fileResultReader.Position += addressLength;//跳过被替换长度
                                tempLength = 0;//取长度（顺带跳过）
                                for (int i = 0; i < addressLength; i++)
                                    tempLength |= (Int64)((fileResultReader.ReadByte() & 0xff) << (8 * i));//读到
                                fileResultReader.Position += tempLength;//跳过元素长度
                                break;

                            case 0x02://删除
                                fileResultReader.Position += addressLength;//跳过地址
                                fileResultReader.Position += addressLength;//跳过被删除长度
                                break;

                            case 0x03://增加
                                fileResultReader.Position += addressLength;//跳过地址
                                tempLength = 0;//取增加长度（顺带跳过）
                                for (int i = 0; i < addressLength; i++)
                                    tempLength |= (Int64)((fileResultReader.ReadByte() & 0xff) << (8 * i));//读到
                                fileResultReader.Position += tempLength;//跳过元素长度
                                break;

                            default:
                                Console.WriteLine(@"参数错误，退出1");
                                return;
                        }
                    }

                    else if (tempType == 0xaa)//单元素替换
                    {
                        byte tempValueA = (byte)fileResultReader.ReadByte();//取主元素
                        Int64 positionCount = 0;//取地址个数
                        for (int i = 0; i < addressLength; i++)
                            positionCount |= (Int64)((fileResultReader.ReadByte() & 0xff) << (i * 8));
                        while (positionCount > 0)//取每个地址
                        {
                            Int64 tempPositionA = 0;
                            for (int i = 0; i < addressLength; i++)
                                tempPositionA |= (Int64)((fileResultReader.ReadByte() & 0xff) << (i * 8));
                            if(tempPositionA>=fileBaseReader.Length)
                            {
                                Console.WriteLine(@"读取到错误长度1");
                                return;
                            }
                            positionCount--;
                            
                            fileList[(int)tempPositionA] = tempValueA;//写入更改值，替换源文件中值
                        }
                    }

                    else if (tempType == 0xbb)//双元素替换
                    {
                        byte tempValueB1 = (byte)fileResultReader.ReadByte();//取主元素
                        byte tempValueB2 = (byte)fileResultReader.ReadByte();//取主元素
                        Int64 positionCount = 0;//取地址个数
                        for (int i = 0; i < addressLength; i++)
                            positionCount |= (Int64)((fileResultReader.ReadByte() & 0xff) << (i * 8));
                        while (positionCount > 0)//取每个地址
                        {
                            Int64 tempPositionB = 0;
                            for (int i = 0; i < addressLength; i++)
                                tempPositionB |= (Int64)((fileResultReader.ReadByte() & 0xff) << (i * 8));
                            if (tempPositionB >= fileBaseReader.Length)
                            {
                                Console.WriteLine(@"读取到错误长度2");
                                return;
                            }
                            positionCount--;
                            
                            fileList[(int)tempPositionB] = tempValueB1;//写入更改值，替换源文件中值
                            fileList[(int)tempPositionB + 1] = tempValueB2;//写入更改值，替换源文件中值
                        }
                    }

                    else
                    {
                        Console.WriteLine(@"元素头搜索错误2");
                        return;
                    }
                }
            }
        }

        static void BuiltFileList()//把FRR里内容合并到result里
        {
            fileResultReader.Position = 33;//去文件头

            {//第二次循环，更改其他元素以及赋值到结果
                int tempType = 0;
                while ((tempType = fileResultReader.ReadByte()) != -1)
                {
                    if (tempType == 0xff)//更改ff部分
                    {
                        switch (fileResultReader.ReadByte())
                        {
                            case 0x01://替换
                                {
                                    int basePosition = 0;
                                    int baseLength = 0;
                                    int resultLentgh = 0;
                                    for (int i = 0; i < addressLength; i++)//被替换地址
                                        basePosition |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));
                                    
                                    for (int i = 0; i < addressLength; i++)//被替换长度
                                        baseLength |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));
                                    for (int i = 0; i < addressLength; i++)//替换长度
                                        resultLentgh |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));

                                    basePosition += diff;
                                    fileList.RemoveRange(basePosition, baseLength);//先删除
                                    diff -= baseLength;

                                    byte[] temp = new byte[resultLentgh];
                                    for (int i = 0; i < resultLentgh; i++)//再添加
                                        temp[i] = (byte)fileResultReader.ReadByte();
                                    fileList.InsertRange(basePosition , temp);
                                    diff += resultLentgh;
                                }
                                break;

                            case 0x02://删除
                                {
                                    int basePosition = 0;
                                    int baseLength = 0;
                                    for (int i = 0; i < addressLength; i++)//被删除地址
                                        basePosition |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));
                                    for (int i = 0; i < addressLength; i++)//被删除长度
                                        baseLength |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));

                                    basePosition += diff;
                                    fileList.RemoveRange(basePosition, baseLength);//删除
                                    diff -= baseLength;
                                }
                                break;

                            case 0x03://增加
                                {
                                    int basePosition = 0;
                                    int resultLentgh = 0;
                                    for (int i = 0; i < addressLength; i++)//被增加地址
                                        basePosition |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));
                                    basePosition += diff;
                                    for (int i = 0; i < addressLength; i++)//增加长度
                                        resultLentgh |= ((fileResultReader.ReadByte() & 0xff) << (8 * i));

                                    byte[] temp = new byte[resultLentgh];
                                    for (int i = 0; i < resultLentgh; i++)//添加新元素
                                        temp[i] = (byte)fileResultReader.ReadByte();
                                    fileList.InsertRange(basePosition , temp);
                                    diff += resultLentgh;
                                    
                                        
                                }
                                break;

                            default:
                                Console.WriteLine(@"参数错误，退出1");
                                return;
                        }
                    }

                    else if (tempType == 0xaa)//单元素替换
                    {
                        byte tempValueA = (byte)fileResultReader.ReadByte();//取主元素
                        Int64 positionCount = 0;//取地址个数
                        for (int i = 0; i < addressLength; i++)
                            positionCount |= (Int64)((fileResultReader.ReadByte() & 0xff) << (i * 8));

                        fileResultReader.Position += (positionCount * addressLength);//跳过地址
                    }

                    else if (tempType == 0xbb)//双元素替换
                    {
                        byte tempValueB1 = (byte)fileResultReader.ReadByte();//取主元素
                        byte tempValueB2 = (byte)fileResultReader.ReadByte();//取主元素
                        Int64 positionCount = 0;//取地址个数
                        for (int i = 0; i < addressLength; i++)
                            positionCount |= (Int64)((fileResultReader.ReadByte() & 0xff) << (i * 8));

                        fileResultReader.Position += (positionCount * addressLength);//跳过地址
                    }

                    else
                    {
                        Console.WriteLine(@"元素头搜索错误2");
                        return;
                    }
                }

                if (fileResultReader.Position > fileResultReader.Length)
                    Console.WriteLine(@"错误3");
            }
        }
        static void WriteToFile()
        {
                
            
            fileWriter.Position = 0;

            for (int i = 0; i < fileList.Count; i++)
                fileWriter.WriteByte(fileList[i]);
        }
    }
}
