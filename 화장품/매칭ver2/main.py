import pandas as pd  # Pandas 라이브러리를 임포트합니다.
from tqdm import tqdm  # tqdm 라이브러리를 추가

# 'E_ingredients.xlsx'와 'K_ingredients.xlsx' 엑셀 파일을 읽어서 데이터프레임으로 저장합니다.
e_dataframe = pd.read_excel('E_ingredients.xlsx')
k_dataframe = pd.read_excel('K_ingredients.xlsx')

# 'KName' 열에 추가할 데이터를 저장할 빈 리스트를 생성합니다.
kname_col = []

# tqdm을 사용하여 반복 작업의 진행 상황을 표시합니다.
for word in tqdm(e_dataframe['Name']):
    # 'E_ingredients.xlsx'의 'Name' 열의 각 항목을 가져와서
    # 'K_ingredients.xlsx' 데이터프레임에서 해당 항목과 일치하는 행을 찾습니다.
    #match = k_dataframe[k_dataframe[' 영문명'] == word]
    match = k_dataframe[k_dataframe[' 영문명'].str.lower() == word.lower()]

    if not match.empty:
        # 만약 일치하는 데이터가 존재하면,
        # 해당 데이터프레임에서 ' 성분명' 열의 값을 가져와서 'kname_col' 리스트에 추가합니다.
        kname_col.append(match.iloc[0][' 성분명'])
    else:
        # 일치하는 데이터가 없으면 빈 문자열을 'kname_col' 리스트에 추가합니다.
        kname_col.append('')

# 'KName' 열을 'E_ingredients.xlsx' 데이터프레임에 추가합니다.
e_dataframe['KName'] = kname_col

# 결과를 'E_ingredients.csv' 파일로 저장합니다.
# 'index=False'는 인덱스를 CSV 파일에 포함하지 않도록 설정하며,
# 'encoding='utf-8-sig''는 UTF-8 인코딩으로 저장하고 BOM(Byte Order Mark)을 추가하도록 설정합니다.
e_dataframe.to_csv('E_ingredients.csv', index=False, encoding='utf-8-sig')