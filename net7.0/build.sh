rm -rf ./build/

mkdir build
cp -r ../vinny-socks5-proxy build
cp -r ../trusts build
rm -rf ./build/*/bin


sed 's/<TargetFrameworkVersion>....<\/TargetFrameworkVersion>/<TargetFramework>net7.0<\/TargetFramework>/g' ./build/trusts/trusts.csproj > ./build/trusts/trusts.csproj2

sed 's/<Project DefaultTargets="Build" ToolsVersion="4.0" xmlns=.*>/<Project Sdk="Microsoft.NET.Sdk">/g' ./build/trusts/trusts.csproj2 > ./build/trusts/trusts.csproj3

sed 's/.*Microsoft.CSharp.targets.*>//g' ./build/trusts/trusts.csproj3 > ./build/trusts/trusts.csproj2

sed 's/.*<Compile Include=".*>//g' ./build/trusts/trusts.csproj2 > ./build/trusts/trusts.csproj3

sed 's/.*<Reference Include="System".*//g' ./build/trusts/trusts.csproj3 > ./build/trusts/trusts.csproj


rm -rf ./build/trusts/Properties

# https://learn.microsoft.com/ru-ru/dotnet/core/tools/dotnet

sed 's/<TargetFrameworkVersion>....<\/TargetFrameworkVersion>/<TargetFramework>net7.0<\/TargetFramework>/g' ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj > ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj2

sed 's/<Project DefaultTargets="Build" ToolsVersion="4.0" xmlns=.*>/<Project Sdk="Microsoft.NET.Sdk">/g' ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj2 > ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj3

sed 's/.*Microsoft.CSharp.targets.*>//g' ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj3 > ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj2

sed 's/.*<Compile Include=".*>//g' ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj2 > ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj3

sed 's/.*<Reference Include="System".*//g' ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj3 > ./build/vinny-socks5-proxy/vinny-socks5-proxy.csproj


rm -rf ../build/*

rm -rf ./build/vinny-socks5-proxy/Properties
rm -rf ./build/arc

rm -rf ../build/arc
mkdir -p ../build/arc/vinny-socks5-proxy &&

dotnet publish ./build/trusts --configuration Release --self-contained false --use-current-runtime false /p:PublishSingleFile=false &&
dotnet publish ./build/vinny-socks5-proxy --configuration Release --self-contained false --use-current-runtime false /p:PublishSingleFile=true &&


mkdir -p ./build/arc/vinny-socks5-proxy &&

cp -f ./build/vinny-socks5-proxy/bin/Release/net7.0/linux-x64/publish/vinny-socks5-proxy ./build/arc/vinny-socks5-proxy &&
#cp -f ./build/vinny-socks5-proxy/bin/Release/net7.0/publish/*.dll ./build/arc/vinny-socks5-proxy &&
#cp -f ./build/vinny-socks5-proxy/bin/Release/net7.0/publish/*.trusts ./build/arc/vinny-socks5-proxy &&
#cp -f ./build/vinny-socks5-proxy/bin/Release/net7.0/publish/*.txt ./build/arc/vinny-socks5-proxy &&
#cp -f ./build/vinny-socks5-proxy/bin/Release/net7.0/publish/*.conf* ./build/arc/vinny-socks5-proxy &&
#cp -f ./build/vinny-socks5-proxy/bin/Release/net7.0/publish/*.json ./build/arc/vinny-socks5-proxy &&

7z a -y -t7z -stl -m0=lzma -mx=9 -ms=on -bb0 -bd -ssc -ssw ../build/vinny-socks5-proxy-net70.7z ./build/arc/vinny-socks5-proxy &&



cp -f ../vinny-socks5-proxy/bin/Release/*.dll ../build/arc/vinny-socks5-proxy &&
cp -f ../vinny-socks5-proxy/bin/Release/*.exe ../build/arc/vinny-socks5-proxy &&
cp -f ../vinny-socks5-proxy/bin/Release/*.trusts ../build/arc/vinny-socks5-proxy &&
cp -f ../vinny-socks5-proxy/bin/Release/*.txt ../build/arc/vinny-socks5-proxy &&
cp -f ../vinny-socks5-proxy/bin/Release/*.conf* ../build/arc/vinny-socks5-proxy &&

7z a -y -t7z -stl -m0=lzma -mx=9 -ms=on -bb0 -bd -ssc -ssw ../build/vinny-socks5-proxy-framework48.7z ../build/arc/vinny-socks5-proxy

