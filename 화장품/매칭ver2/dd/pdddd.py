import pandas as pd

# 파일 경로 (여기서는 예시로 'your_file.xlsx'라고 지정합니다.)
file_path = '할랄 화장품 전성분 500.xlsx'

# Excel 파일 읽기
df = pd.read_excel(file_path)

# 'P_ID'와 'B_ID' 초기화
p_id = 0
b_id = 0
last_brand = None


# 각 행에 대해 브랜드 이름이 변경될 때마다 'P_ID'와 'B_ID' 업데이트
for index, row in df.iterrows():
    if row['Brand'] != last_brand:
        p_id += 1
        b_id += 1
        last_brand = row['Brand']
    df.at[index, 'P_ID'] = f'P_{p_id}'
    df.at[index, 'B_ID'] = f'B_{b_id}'

# 결과 파일 저장 경로 (여기서는 예시로 'updated_file.xlsx'라고 지정합니다.)
result_file_path = 'updated_file.xlsx'
df.to_excel(result_file_path, index=False)
