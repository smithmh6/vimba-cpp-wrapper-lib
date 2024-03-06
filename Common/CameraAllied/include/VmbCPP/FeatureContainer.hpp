/*=============================================================================
  Copyright (C) 2012 Allied Vision Technologies.  All Rights Reserved.

  Redistribution of this file, in original or modified form, without
  prior written consent of Allied Vision Technologies is prohibited.

-------------------------------------------------------------------------------

  File:        FeatureContainer.hpp

  Description: Inline wrapper functions for class 
               VmbCPP::FeatureContainer.

-------------------------------------------------------------------------------

  THIS SOFTWARE IS PROVIDED BY THE AUTHOR "AS IS" AND ANY EXPRESS OR IMPLIED
  WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF TITLE,
  NON-INFRINGEMENT, MERCHANTABILITY AND FITNESS FOR A PARTICULAR  PURPOSE ARE
  DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, 
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED  
  AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR 
  TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

=============================================================================*/

#ifndef VMBCPP_FEATURECONTAINER_HPP
#define VMBCPP_FEATURECONTAINER_HPP

/**
* \file  FeatureContainer.hpp
*
* \brief Inline wrapper functions for class VmbCPP::FeatureContainer
*        that allocate memory for STL objects in the application's context
*        and to pass data across DLL boundaries using arrays
*/

namespace VmbCPP {

// HINT: Once queried this information remains static throughout the object's lifetime
inline VmbErrorType FeatureContainer::GetFeatures( FeaturePtrVector &features )
{
    VmbErrorType    res;
    VmbUint32_t     nSize;

    res = GetFeatures( nullptr, nSize );
    if (    VmbErrorSuccess == res )
    {
        if( 0 != nSize)
        {
            try
            {
                FeaturePtrVector tmpFeatures( nSize );
                res = GetFeatures( &tmpFeatures[0], nSize );
                if( VmbErrorSuccess == res)
                {
                    features.swap( tmpFeatures );
                }
            }
            catch(...)
            {
                return VmbErrorResources;
            }
        }
        else
        {
            features.clear();
        }
    }

    return res;
}

}  // namespace VmbCPP

#endif
