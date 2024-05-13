import pandas as pd

# 파일 경로 (예시: 'your_file.xlsx')
file_path = '할랄 화장품 전성분 500.xlsx'

# Excel 파일 읽기
df = pd.read_excel(file_path)

# ID 초기화 및 이전 브랜드 이름 저장
p_id = -1
b_id = -1
last_brand = None

# 'P_ID'와 'B_ID' 컬럼을 초기화
df['P_ID'] = ''
df['B_ID'] = ''

# 브랜드 이름이 변경될 때마다 'P_ID'와 'B_ID' 값을 업데이트
for index, row in df.iterrows():
    if row['Brand'] != last_brand:
        p_id += 1
        b_id += 1
        last_brand = row['Brand']
    df.at[index, 'P_ID'] = f'P_{p_id}'
    df.at[index, 'B_ID'] = f'B_{b_id}'

# 결과 파일 저장 경로 (예시: 'updated_file.xlsx')
result_file_path = '할랄 화장품 전성분 500_UPpppp.xlsx'
df.to_excel(result_file_path, index=False)
