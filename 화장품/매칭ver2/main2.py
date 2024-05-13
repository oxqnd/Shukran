import pandas as pd
from tqdm import tqdm  # tqdm 라이브러리를 추가
# tqdm을 사용하여 진행 상황을 표시

e_dataframe = pd.read_excel('E_ingredients.xlsx')
k_dataframe = pd.read_excel('K_ingredients.xlsx')

kname_col = []

# tqdm을 사용하여 진행 상황을 표시
for word in tqdm(e_dataframe['Name']):
    matches = []
    for k_word in k_dataframe[' 영문명'].str.split('|'):
        if isinstance(k_word, list) and word in k_word:
            matching_rows = k_dataframe[k_dataframe[' 영문명'].str.contains(word, na=False)]
            if not matching_rows.empty:
                matches.append(matching_rows.iloc[0][' 성분명'])
    if matches:
        kname_col.append('|'.join(matches))
    else:
        kname_col.append('')


        
e_dataframe['KName'] = kname_col
e_dataframe.to_csv('E_ingredients.csv', index=False, encoding='utf-8-sig')