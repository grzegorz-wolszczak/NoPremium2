#!/usr/bin/env bash


base_dir="/media/gary/D_DRIVE/repos/NoPremium2/src/NoPremium2"
bin_dir="bin/Debug/net10.0"
app_name="NoPremium2"


config_file_path="${HOME}/nopremium.config.json"

full_path="${base_dir}/${bin_dir}/${app_name}"
if [[ ! -f ${full_path} ]]; then
    echo "Error no such file ${full_path}"
    exit 1
fi
echo "Running..."
cd $base_dir
dotnet run ${base_dir} -- ${HOME}/nopremium.config.json
